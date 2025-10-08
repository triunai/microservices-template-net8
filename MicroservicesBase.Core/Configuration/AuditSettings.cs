namespace MicroservicesBase.Core.Configuration;

/// <summary>
/// Configuration settings for audit logging system
/// </summary>
public sealed class AuditSettings
{
    public const string SectionName = "AuditSettings";
    
    /// <summary>
    /// Master switch for audit logging. Set to false to disable entirely.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Storage configuration
    /// </summary>
    public StorageSettings Storage { get; set; } = new();
    
    /// <summary>
    /// Payload logging configuration
    /// </summary>
    public PayloadSettings Payloads { get; set; } = new();
    
    /// <summary>
    /// Sampling configuration for high-volume operations
    /// </summary>
    public SamplingSettings Sampling { get; set; } = new();
    
    /// <summary>
    /// Queue and batching configuration
    /// </summary>
    public QueueSettings Queue { get; set; } = new();
    
    /// <summary>
    /// Retention policy
    /// </summary>
    public RetentionSettings Retention { get; set; } = new();
    
    /// <summary>
    /// Backpressure handling
    /// </summary>
    public BackpressureSettings Backpressure { get; set; } = new();
}

public sealed class StorageSettings
{
    /// <summary>
    /// Storage type: PerTenantDatabase or MasterDatabase
    /// </summary>
    public string Type { get; set; } = "PerTenantDatabase";
    
    /// <summary>
    /// Table name for audit logs
    /// </summary>
    public string TableName { get; set; } = "AuditLog";
}

public sealed class PayloadSettings
{
    /// <summary>
    /// Log request payloads (can contain PII)
    /// </summary>
    public bool LogRequests { get; set; } = true;
    
    /// <summary>
    /// Log response payloads (can contain PII)
    /// </summary>
    public bool LogResponses { get; set; } = true;
    
    /// <summary>
    /// Compress payloads using Gzip before storing
    /// </summary>
    public bool Compress { get; set; } = true;
    
    /// <summary>
    /// Encrypt payloads using AES-256-GCM (requires encryption keys in config)
    /// </summary>
    public bool Encrypt { get; set; } = false;
    
    /// <summary>
    /// Maximum payload size in KB before truncation
    /// </summary>
    public int MaxSizeKB { get; set; } = 256;
    
    /// <summary>
    /// Fields to include in payload (whitelist). Empty = include all.
    /// </summary>
    public List<string> WhitelistFields { get; set; } = new();
    
    /// <summary>
    /// Fields to mask in payload (e.g., email, phone, cardNumber)
    /// </summary>
    public List<string> MaskFields { get; set; } = new() { "email", "phone", "cardNumber" };
}

public sealed class SamplingSettings
{
    /// <summary>
    /// Percentage of read operations to log (1-100). 100 = log all reads.
    /// </summary>
    public int ReadsPercent { get; set; } = 10;
    
    /// <summary>
    /// Percentage of write operations to log (1-100). Should always be 100.
    /// </summary>
    public int WritesPercent { get; set; } = 100;
}

public sealed class QueueSettings
{
    /// <summary>
    /// Maximum number of audit entries in the in-memory queue
    /// </summary>
    public int Capacity { get; set; } = 10000;
    
    /// <summary>
    /// Number of entries to write in a single batch
    /// </summary>
    public int BatchSize { get; set; } = 200;
    
    /// <summary>
    /// Flush interval in seconds (write batches even if not full)
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 5;
}

public sealed class RetentionSettings
{
    /// <summary>
    /// Number of days to retain audit logs before auto-deletion
    /// </summary>
    public int Days { get; set; } = 90;
    
    /// <summary>
    /// Cron expression for purge job schedule (default: 2 AM daily)
    /// </summary>
    public string PurgeSchedule { get; set; } = "0 2 * * *";
}

public sealed class BackpressureSettings
{
    /// <summary>
    /// Drop payload data (keep metadata only) when under backpressure
    /// </summary>
    public bool DropPayloads { get; set; } = true;
    
    /// <summary>
    /// Fallback to Serilog file sink if database is unavailable
    /// </summary>
    public bool FallbackToFile { get; set; } = true;
}

