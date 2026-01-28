using Rgt.Space.Core.Debugging;

namespace Rgt.Space.Core.Abstractions.Debugging;

/// <summary>
/// Records combo break events for later analysis (dev-only black box).
/// In production, use <see cref="NullComboBreakRecorder"/> to avoid memory usage.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> Implementations must be thread-safe for concurrent recording.
/// </para>
/// <para>
/// <b>Bounding:</b> Implementations should limit storage to prevent unbounded memory growth.
/// </para>
/// </remarks>
public interface IComboBreakRecorder
{
    /// <summary>
    /// Records a combo break snapshot.
    /// </summary>
    void Record(ComboBreakSnapshot snapshot);
    
    /// <summary>
    /// Gets all recorded snapshots (most recent last).
    /// </summary>
    IReadOnlyList<ComboBreakSnapshot> GetAll();
    
    /// <summary>
    /// Looks up a snapshot by correlation ID.
    /// </summary>
    ComboBreakSnapshot? GetByCorrelationId(string correlationId);
    
    /// <summary>
    /// Number of recorded snapshots currently in memory.
    /// </summary>
    int Count { get; }
}
