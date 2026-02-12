namespace Rgt.Space.Core.Debugging;

/// <summary>
/// Immutable snapshot of a "combo break" event for debugging purposes.
/// Captures the state at the moment of failure for later analysis.
/// </summary>
/// <remarks>
/// <para>
/// <b>PII Guardrails:</b> This record intentionally excludes sensitive data.
/// UserId is stored as a safe GUID identifier. Never store tokens, passwords,
/// request bodies, or SQL parameters.
/// </para>
/// <para>
/// <b>Low Cardinality:</b> Checkpoint names should be code references (handler/step names),
/// not user data. No GUIDs, timestamps, or dynamic values in checkpoint names.
/// </para>
/// </remarks>
public sealed record ComboBreakSnapshot
{
    /// <summary>
    /// Unique identifier for the request (from X-Correlation-Id header).
    /// </summary>
    public required string CorrelationId { get; init; }
    
    /// <summary>
    /// UTC timestamp when the failure occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
    
    /// <summary>
    /// The checkpoint being attempted at failure time (the break point).
    /// May be null if failure occurred before handler entry.
    /// </summary>
    public string? CheckpointCurrent { get; init; }
    
    /// <summary>
    /// The last successfully completed checkpoint.
    /// Immutable after failure — represents last known good state.
    /// </summary>
    public string? CheckpointLast { get; init; }
    
    /// <summary>
    /// OpenTelemetry W3C trace ID (if Activity exists).
    /// Maps to the traceparent header's trace-id field.
    /// </summary>
    public string? TraceId { get; init; }
    
    /// <summary>
    /// Current span ID (if Activity exists).
    /// Maps to the traceparent header's parent-id field.
    /// </summary>
    public string? SpanId { get; init; }
    
    /// <summary>
    /// HTTP route path (e.g., "/api/v1/users").
    /// </summary>
    public required string Route { get; init; }
    
    /// <summary>
    /// MediatR handler name (raw, not the checkpoint string).
    /// </summary>
    public required string Handler { get; init; }
    
    /// <summary>
    /// Error code from the ErrorCatalog (e.g., "SALE_NOT_FOUND").
    /// </summary>
    public required string ErrorCode { get; init; }
    
    /// <summary>
    /// Error message (exception message or FluentResults error).
    /// </summary>
    public required string ErrorMessage { get; init; }
    
    /// <summary>
    /// Tenant identifier (if multi-tenant context is active).
    /// </summary>
    public string? TenantId { get; init; }
    
    /// <summary>
    /// User identifier (safe GUID, no PII).
    /// </summary>
    public Guid? UserId { get; init; }
    
    /// <summary>
    /// Combo position information (Phase 3).
    /// Null if no combo is declared for this handler or ComboMeter is disabled.
    /// </summary>
    public ComboPosition? ComboPosition { get; init; }
}
