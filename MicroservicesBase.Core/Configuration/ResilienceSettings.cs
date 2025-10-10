namespace MicroservicesBase.Core.Configuration;

/// <summary>
/// Configuration settings for Polly v8 resilience pipelines.
/// Supports hot-reload for runtime tuning without redeploy.
/// </summary>
public sealed class ResilienceSettings
{
    public const string SectionName = "ResilienceSettings";
    
    /// <summary>
    /// Master tenant database pipeline settings (cached, rare calls)
    /// </summary>
    public PipelineSettings MasterDb { get; set; } = new();
    
    /// <summary>
    /// Redis cache pipeline settings (fail-fast to DB fallback)
    /// </summary>
    public PipelineSettings Redis { get; set; } = new();
    
    /// <summary>
    /// Per-tenant database pipeline settings (high traffic)
    /// </summary>
    public PipelineSettings TenantDb { get; set; } = new();
    
    /// <summary>
    /// Audit database pipeline settings (async, non-critical)
    /// </summary>
    public PipelineSettings AuditDb { get; set; } = new();
}

/// <summary>
/// Pipeline-specific resilience settings for timeout, retry, and circuit breaker
/// </summary>
public sealed class PipelineSettings
{
    /// <summary>
    /// Total timeout budget for the entire operation (outermost strategy)
    /// </summary>
    public int TimeoutMs { get; set; }
    
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// Array of retry delay values in milliseconds (jitter will be applied ±25%)
    /// Length should match RetryCount. Example: [75, 150, 300] for 3 retries.
    /// </summary>
    public int[] RetryDelaysMs { get; set; } = [];
    
    /// <summary>
    /// Failure ratio threshold for circuit breaker (0.0 to 1.0)
    /// Example: 0.5 = 50% failure rate triggers breaker open
    /// </summary>
    public double FailureRatio { get; set; }
    
    /// <summary>
    /// Sampling duration for failure ratio calculation (seconds)
    /// </summary>
    public int SamplingDurationSeconds { get; set; }
    
    /// <summary>
    /// Minimum number of requests in sampling window before breaker can open
    /// </summary>
    public int MinimumThroughput { get; set; }
    
    /// <summary>
    /// Duration to keep breaker open before attempting half-open (seconds)
    /// </summary>
    public int BreakDurationSeconds { get; set; }
    
    /// <summary>
    /// Optional: Maximum concurrent operations per tenant (bulkhead pattern)
    /// Null = no bulkhead limit
    /// </summary>
    public int? BulkheadMaxConcurrency { get; set; }
}
