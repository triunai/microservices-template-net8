namespace Rgt.Space.Core.Constants;

/// <summary>
/// Cache-related constants (keys, prefixes, TTLs).
/// Centralizes all caching magic values for consistency.
/// </summary>
public static class CacheConstants
{
    /// <summary>
    /// Cache key prefixes for different entity types
    /// </summary>
    public static class KeyPrefixes
    {
        /// <summary>
        /// Base instance name for Redis keys
        /// </summary>
        public const string Instance = "MicroservicesBase:";
        
        /// <summary>
        /// Prefix for tenant connection string cache keys
        /// Format: "tenant:connectionstring:{tenantId}"
        /// </summary>
        public const string TenantConnectionString = "tenant:connectionstring:";
        
        /// <summary>
        /// Prefix for sale entity cache keys
        /// Format: "sale:{saleId}"
        /// </summary>
        public const string Sale = "sale:";
        
        /// <summary>
        /// Prefix for product catalog cache keys
        /// Format: "product:{productId}"
        /// </summary>
        public const string Product = "product:";
    }

    /// <summary>
    /// Default TTL values (in seconds)
    /// </summary>
    public static class DefaultTtl
    {
        public const int TenantConnectionString = 600;  // 10 minutes
        public const int Sale = 300;                     // 5 minutes
        public const int Product = 3600;                 // 1 hour
        public const int Session = 1800;                 // 30 minutes
    }
}

