using MicroservicesBase.Core.Domain.Auditing;

namespace MicroservicesBase.Core.Abstractions.Auditing;

/// <summary>
/// Abstraction for audit logging service.
/// Implementations should be thread-safe and handle backpressure gracefully.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Enqueue an audit entry for asynchronous persistence.
    /// This method should NOT block the calling thread.
    /// </summary>
    /// <param name="entry">The audit entry to log</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task that completes when entry is enqueued (not necessarily persisted)</returns>
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
    
    /// <summary>
    /// Flush all pending audit entries to storage.
    /// Typically called on graceful shutdown to ensure no logs are lost.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task that completes when all pending entries are persisted</returns>
    Task FlushAsync(CancellationToken ct = default);
}

