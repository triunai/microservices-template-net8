using MicroservicesBase.Core.Abstractions.Tenancy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace MicroservicesBase.Infrastructure.Tenancy
{
    /// <summary>
    /// Enhanced version with cache stampede protection using SemaphoreSlim.
    /// Only ONE request per tenant can refresh the cache at a time.
    /// Other requests wait for the first one to complete, then read from cache.
    /// </summary>
    public sealed class CachedTenantConnectionFactoryWithStampedeProtection : ITenantConnectionFactory
    {
        private readonly MasterTenantConnectionFactory _innerFactory;
        private readonly IDistributedCache _cache;
        private readonly ILogger<CachedTenantConnectionFactoryWithStampedeProtection> _logger;
        private readonly TimeSpan _cacheTTL;
        private const string CacheKeyPrefix = "tenant:connectionstring:";
        
        // One semaphore per tenant - prevents multiple requests from hitting DB simultaneously
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public CachedTenantConnectionFactoryWithStampedeProtection(
            MasterTenantConnectionFactory innerFactory,
            IDistributedCache cache,
            IConfiguration configuration,
            ILogger<CachedTenantConnectionFactoryWithStampedeProtection> logger)
        {
            _innerFactory = innerFactory;
            _cache = cache;
            _logger = logger;
            
            var ttlString = configuration["CacheSettings:TenantConnectionStringTTL"];
            var ttlSeconds = int.TryParse(ttlString, out var parsed) ? parsed : 600;
            _cacheTTL = TimeSpan.FromSeconds(ttlSeconds);
        }

        public async Task<string> GetSqlConnectionStringAsync(string tenantId, CancellationToken ct = default)
        {
            var cacheKey = $"{CacheKeyPrefix}{tenantId}";

            // Try cache first (fast path)
            try
            {
                var cachedValue = await _cache.GetAsync(cacheKey, ct);
                if (cachedValue != null)
                {
                    var connectionString = Encoding.UTF8.GetString(cachedValue);
                    _logger.LogDebug("Cache HIT for tenant: {TenantId}", tenantId);
                    return connectionString;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache error for tenant {TenantId}, falling back to database", tenantId);
            }

            // Cache miss - acquire lock for this tenant
            var semaphore = _locks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
            
            await semaphore.WaitAsync(ct);
            try
            {
                // Double-check cache after acquiring lock
                // (another request might have already refreshed it)
                try
                {
                    var cachedValue = await _cache.GetAsync(cacheKey, ct);
                    if (cachedValue != null)
                    {
                        var connectionString = Encoding.UTF8.GetString(cachedValue);
                        _logger.LogDebug("Cache HIT after lock (another request refreshed it) for tenant: {TenantId}", tenantId);
                        return connectionString;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Redis cache error after lock for tenant {TenantId}", tenantId);
                }

                _logger.LogDebug("Cache MISS for tenant: {TenantId}, querying master DB (PROTECTED)", tenantId);

                // Get from database (only ONE request per tenant executes this)
                var dbConnectionString = await _innerFactory.GetSqlConnectionStringAsync(tenantId, ct);

                // Cache the result
                try
                {
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _cacheTTL
                    };

                    var bytes = Encoding.UTF8.GetBytes(dbConnectionString);
                    await _cache.SetAsync(cacheKey, bytes, cacheOptions, ct);
                    
                    _logger.LogInformation("Cached connection string for tenant: {TenantId} (TTL: {TTL}s, STAMPEDE PROTECTED)", 
                        tenantId, _cacheTTL.TotalSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache connection string for tenant {TenantId}", tenantId);
                }

                return dbConnectionString;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task InvalidateCacheAsync(string tenantId, CancellationToken ct = default)
        {
            var cacheKey = $"{CacheKeyPrefix}{tenantId}";
            
            try
            {
                await _cache.RemoveAsync(cacheKey, ct);
                _logger.LogInformation("Cache invalidated for tenant: {TenantId}", tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate cache for tenant {TenantId}", tenantId);
            }
        }
    }
}




