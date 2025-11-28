namespace Rgt.Space.Core.Constants;

/// <summary>
/// Polly resilience pipeline constants (keys, defaults).
/// Centralizes all resilience-related magic values.
/// </summary>
public static class ResilienceConstants
{
    /// <summary>
    /// Resilience pipeline keys (used for DI registration and retrieval)
    /// </summary>
    public static class PipelineKeys
    {
        /// <summary>
        /// Master database resilience pipeline (tenant connection string lookup)
        /// </summary>
        public const string MasterDb = "MasterDb";
        
        /// <summary>
        /// Redis cache resilience pipeline (distributed caching)
        /// </summary>
        public const string Redis = "Redis";
        
        /// <summary>
        /// Tenant database resilience pipeline prefix
        /// Format: "TenantDb:{tenantId}"
        /// </summary>
        public const string TenantDbPrefix = "TenantDb";
        
        /// <summary>
        /// Audit database resilience pipeline prefix
        /// Format: "AuditDb:{tenantId}"
        /// </summary>
        public const string AuditDbPrefix = "AuditDb";
    }

    /// <summary>
    /// Default timeout values (in milliseconds)
    /// </summary>
    public static class DefaultTimeouts
    {
        public const int Redis = 200;           // Fast-fail for cache
        public const int MasterDb = 700;        // Tenant lookup
        public const int TenantDb = 1500;       // Read operations
        public const int AuditDb = 4000;        // Background writes (can be slower)
        public const int CacheWarmup = 100;     // Async cache warming (fire-and-forget)
    }

    /// <summary>
    /// Circuit breaker defaults
    /// </summary>
    public static class CircuitBreaker
    {
        public const double DefaultFailureRatio = 0.5;        // 50% failures opens circuit
        public const int DefaultSamplingDuration = 30;        // 30 seconds
        public const int DefaultMinimumThroughput = 10;       // Minimum requests before CB activates
        public const int DefaultBreakDuration = 10;           // 10 seconds before trying again
    }

    /// <summary>
    /// Retry defaults
    /// </summary>
    public static class Retry
    {
        public const int DefaultMaxAttempts = 3;
        public const int FallbackDelayMs = 500;              // Used when delay array is exhausted
    }
}

