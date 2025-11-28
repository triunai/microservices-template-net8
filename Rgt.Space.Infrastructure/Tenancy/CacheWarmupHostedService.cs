using Dapper;
using Rgt.Space.Core.Abstractions.Tenancy;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Rgt.Space.Infrastructure.Tenancy;

/// <summary>
/// Pre-warms the IMemoryCache at startup by dynamically loading all active tenant 
/// connection strings from TenantMaster database.
/// 
/// <para>
/// <b>Why This Matters:</b> Without warmup, the first request per tenant triggers:
/// <list type="number">
///   <item>IMemoryCache miss (GetOrCreateAsync starts factory)</item>
///   <item>MasterDb query with Polly pipeline (700ms timeout + 3 retries = 3-6s)</item>
///   <item>Concurrent requests block on GetOrCreateAsync (thread-safe stampede protection)</item>
///   <item>Under 200 req/s load, hundreds of requests pile up waiting</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>With Warmup:</b>
/// <list type="bullet">
///   <item>Cache pre-populated at startup (~3-6s one-time cost per tenant)</item>
///   <item>All requests hit cache (&lt;10ms)</item>
///   <item>No blocking, no stampede</item>
///   <item>Production-ready! ✅</item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Discovery Strategy:</b> Dynamically queries TenantMaster database for all active tenants.
/// NO hardcoding required! Add new tenants to TenantMaster and they're automatically included.
/// </para>
/// 
/// <para>
/// <b>Production Patterns:</b>
/// <list type="bullet">
///   <item>Kubernetes: Init containers pre-warm before accepting traffic</item>
///   <item>Azure App Service: Startup scripts or IHostedService (this pattern)</item>
///   <item>AWS ECS: Health check delays until warmup completes</item>
/// </list>
/// </para>
/// </summary>
public sealed class CacheWarmupHostedService : IHostedService
{
    private readonly ITenantConnectionFactory _connFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CacheWarmupHostedService> _logger;

    public CacheWarmupHostedService(
        ITenantConnectionFactory connFactory,
        IConfiguration config,
        ILogger<CacheWarmupHostedService> logger)
    {
        _connFactory = connFactory;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔥 Starting cache warmup (dynamic tenant discovery)...");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var warmedCount = 0;
        var failedCount = 0;

        try
        {
            // STEP 1: Query TenantMaster for all active tenants (NO HARDCODING!)
            var masterConn = _config.GetConnectionString("TenantMaster");
            
            if (string.IsNullOrEmpty(masterConn))
            {
                _logger.LogWarning("⚠️  TenantMaster connection string not configured - skipping cache warmup");
                return;
            }
            
            await using var conn = new NpgsqlConnection(masterConn);
            await conn.OpenAsync(cancellationToken);
            
            // 🎯 MAGIC: Dynamically discovers all tenants from database!
            var tenantIds = await conn.QueryAsync<string>(
                "SELECT code FROM tenants WHERE status = 'Active'",
                commandTimeout: 5); // 5s timeout for warmup query
            
            var tenantList = tenantIds.ToList();
            
            if (!tenantList.Any())
            {
                _logger.LogWarning("⚠️  No active tenants found in TenantMaster - nothing to warm");
                return;
            }
            
            _logger.LogInformation("Found {TenantCount} active tenant(s) to warm: {Tenants}", 
                tenantList.Count, string.Join(", ", tenantList));
            
            // STEP 2: Pre-warm cache for each tenant (sequentially to avoid DB overload)
            foreach (var tenantId in tenantList)
            {
                try
                {
                    // This calls CachedTenantConnectionFactoryWithStampedeProtection
                    // → which calls MasterTenantConnectionFactory internally
                    // → and populates IMemoryCache[tenantId] = connection string
                    await _connFactory.GetSqlConnectionStringAsync(tenantId, cancellationToken);
                    
                    warmedCount++;
                    _logger.LogInformation("✅ Pre-warmed cache for tenant: {TenantId} ({Current}/{Total})", 
                        tenantId, warmedCount, tenantList.Count);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogWarning(ex, "⚠️  Failed to warm cache for tenant: {TenantId} - first request may be slow", tenantId);
                    // Continue with other tenants (non-fatal - app can still start)
                }
            }
        }
        catch (NpgsqlException pgEx)
        {
            _logger.LogError(pgEx, 
                "❌ Cache warmup failed: Cannot connect to TenantMaster database. " +
                "Application will start but first requests will be slow (3-6s latency per tenant).");
            // Don't throw - allow app to start even if warmup fails
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "❌ Cache warmup failed with unexpected error. " +
                "Application will start but first requests may be slow.");
            // Don't throw - allow app to start even if warmup fails
        }
        
        stopwatch.Stop();
        
        if (warmedCount > 0 || failedCount > 0)
        {
            _logger.LogInformation(
                "🔥 Cache warmup completed in {ElapsedMs}ms: {WarmedCount} succeeded, {FailedCount} failed",
                stopwatch.ElapsedMilliseconds, warmedCount, failedCount);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cache warmup service stopped");
        return Task.CompletedTask;
    }
}

