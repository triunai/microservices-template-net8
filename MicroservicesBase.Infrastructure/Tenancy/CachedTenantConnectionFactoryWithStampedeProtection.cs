using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Core.Constants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MicroservicesBase.Infrastructure.Tenancy
{
    /// <summary>
    /// In-memory cached tenant connection factory (no distributed caching).
    /// Uses IMemoryCache for fast, thread-safe, in-process caching of tenant connection strings.
    /// Perfect for metadata that changes rarely but needs fast access.
    /// No semaphore needed - IMemoryCache.GetOrCreateAsync is thread-safe and handles stampede protection.
    /// </summary>
    public sealed class CachedTenantConnectionFactoryWithStampedeProtection : ITenantConnectionFactory
    {
        private readonly MasterTenantConnectionFactory _innerFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedTenantConnectionFactoryWithStampedeProtection> _logger;
        private readonly TimeSpan _cacheTTL;

        public CachedTenantConnectionFactoryWithStampedeProtection(
            MasterTenantConnectionFactory innerFactory,
            IMemoryCache cache,
            ILogger<CachedTenantConnectionFactoryWithStampedeProtection> logger)
        {
            _innerFactory = innerFactory;
            _cache = cache;
            _logger = logger;
            _cacheTTL = TimeSpan.FromMinutes(10); // 10 minutes TTL for connection strings
            
            _logger.LogInformation("CachedTenantConnectionFactory initialized with in-memory cache (TTL: {TTL}s)", _cacheTTL.TotalSeconds);
        }

        public async Task<string> GetSqlConnectionStringAsync(string tenantId, CancellationToken ct = default)
        {
            // IMemoryCache.GetOrCreateAsync is thread-safe and handles concurrent access automatically
            // No semaphore needed - built-in stampede protection ensures only ONE DB query per key
            return await _cache.GetOrCreateAsync(
                tenantId, // Use tenant ID as cache key (simple and clean)
                async entry =>
                {
                    // Configure cache entry
                    entry.AbsoluteExpirationRelativeToNow = _cacheTTL;
                    entry.Size = 1; // Estimate size (for memory pressure eviction)
                    entry.Priority = CacheItemPriority.High; // Less likely to be evicted under memory pressure
                    
                    // Query database (only happens on cache miss - first request or after expiration)
                    _logger.LogDebug("Tenant {TenantId} [cache=miss, storage=memory, fetching=db]", tenantId);
                    var connectionString = await _innerFactory.GetSqlConnectionStringAsync(tenantId, ct);
                    
                    _logger.LogInformation("Cached connection string for tenant {TenantId} in memory [ttl={TTL}s, storage=memory]", 
                        tenantId, _cacheTTL.TotalSeconds);
                    
                    return connectionString;
                }) 
                ?? throw new InvalidOperationException($"Failed to resolve connection string for tenant {tenantId}");
        }

        public Task InvalidateCacheAsync(string tenantId, CancellationToken ct = default)
        {
            // IMemoryCache.Remove is synchronous (in-process, no network)
            _cache.Remove(tenantId);
            _logger.LogInformation("Cache invalidated for tenant {TenantId} [storage=memory]", tenantId);
            return Task.CompletedTask;
        }
    }
}




