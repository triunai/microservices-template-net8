using Rgt.Space.Core.Abstractions.Debugging;
using Rgt.Space.Core.Debugging;

namespace Rgt.Space.Infrastructure.Debugging;

/// <summary>
/// No-op recorder for production environments.
/// Has zero memory overhead — all operations are no-ops.
/// </summary>
/// <remarks>
/// Use this implementation in production to avoid storing combo break snapshots.
/// The debugging headers and logs still work; only the in-memory recorder is disabled.
/// </remarks>
public sealed class NullComboBreakRecorder : IComboBreakRecorder
{
    /// <inheritdoc />
    public int Count => 0;
    
    /// <inheritdoc />
    public void Record(ComboBreakSnapshot snapshot)
    {
        // No-op in production
    }
    
    /// <inheritdoc />
    public IReadOnlyList<ComboBreakSnapshot> GetAll()
        => Array.Empty<ComboBreakSnapshot>();
    
    /// <inheritdoc />
    public ComboBreakSnapshot? GetByCorrelationId(string correlationId)
        => null;
}
