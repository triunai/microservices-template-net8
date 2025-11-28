namespace Rgt.Space.Core.Domain.Auditing;

/// <summary>
/// Represents a single audit log entry capturing who did what, when, where, and the result.
/// Immutable record for thread-safety in queuing scenarios.
/// </summary>
// Mapped from SQL-AuditLogging table
public sealed record AuditEntry
{
    // ==========================================
    // WHO (Actor)
    // ==========================================
    
    /// <summary>
    /// Tenant identifier (e.g., "7ELEVEN", "BURGERKING")
    /// </summary>
    public required string TenantId { get; init; }
    
    /// <summary>
    /// User identifier from JWT 'sub' claim. Null for unauthenticated requests.
    /// </summary>
    public string? UserId { get; init; }
    
    /// <summary>
    /// Client/Application identifier for machine-to-machine calls
    /// </summary>
    public string? ClientId { get; init; }
    
    /// <summary>
    /// Client IP address (X-Forwarded-For aware)
    /// </summary>
    public string? IpAddress { get; init; }
    
    /// <summary>
    /// User agent string (browser/app identifier)
    /// </summary>
    public string? UserAgent { get; init; }
    
    // ==========================================
    // WHAT (Action)
    // ==========================================
    
    /// <summary>
    /// Action taxonomy: {Entity}.{Verb} (e.g., "Sales.Read", "Sales.Create", "Sales.Void")
    /// </summary>
    public required string Action { get; init; }
    
    /// <summary>
    /// Entity type being operated on (e.g., "Sale", "Customer", "Product")
    /// </summary>
    public string? EntityType { get; init; }
    
    /// <summary>
    /// Specific entity ID being operated on (e.g., sale GUID)
    /// </summary>
    public string? EntityId { get; init; }
    
    // ==========================================
    // WHEN/WHERE (Context)
    // ==========================================
    
    /// <summary>
    /// When the operation occurred (UTC)
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Correlation ID for distributed tracing (X-Correlation-Id header)
    /// </summary>
    public string? CorrelationId { get; init; }
    
    /// <summary>
    /// HTTP request path (e.g., "/api/sales/123")
    /// </summary>
    public string? RequestPath { get; init; }
    
    // ==========================================
    // RESULT (Outcome)
    // ==========================================
    
    /// <summary>
    /// Did the operation succeed?
    /// </summary>
    public required bool IsSuccess { get; init; }
    
    /// <summary>
    /// HTTP status code (200, 404, 500, etc.)
    /// </summary>
    public int? StatusCode { get; init; }
    
    /// <summary>
    /// Business error code (e.g., "SALE_NOT_FOUND")
    /// </summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Request duration in milliseconds
    /// </summary>
    public int? DurationMs { get; init; }
    
    // ==========================================
    // PAYLOADS (Compressed)
    // ==========================================
    
    /// <summary>
    /// Request payload (Gzipped JSON as byte array). Nullable for high-volume reads.
    /// </summary>
    public byte[]? RequestData { get; init; }
    
    /// <summary>
    /// Response payload (Gzipped JSON as byte array). Nullable for high-volume reads.
    /// </summary>
    public byte[]? ResponseData { get; init; }
    
    /// <summary>
    /// For writes: before/after delta (Gzipped JSON as byte array)
    /// </summary>
    public byte[]? Delta { get; init; }
    
    // ==========================================
    // METADATA
    // ==========================================
    
    /// <summary>
    /// Idempotency key for write deduplication
    /// </summary>
    public string? IdempotencyKey { get; init; }
    
    /// <summary>
    /// Source of the operation: API, Job, Migration
    /// </summary>
    public string Source { get; init; } = "API";
    
    /// <summary>
    /// SHA256 hash of request for optional deduplication
    /// </summary>
    public string? RequestHash { get; init; }
}

