using System.Collections.Concurrent;
using Rgt.Space.Core.Abstractions.Debugging;
using Rgt.Space.Core.Debugging;

namespace Rgt.Space.Infrastructure.Debugging;

/// <summary>
/// In-memory recorder for combo break events (dev-only black box).
/// Uses a bounded concurrent queue to store recent failures.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> Uses <see cref="ConcurrentQueue{T}"/> and 
/// <see cref="Interlocked"/> for lock-free concurrent access.
/// </para>
/// <para>
/// <b>Bounding:</b> Evicts oldest entries when capacity is exceeded.
/// Default max size is 100 entries.
/// </para>
/// <para>
/// <b>Dev-Only:</b> Register <see cref="NullComboBreakRecorder"/> in production
/// to avoid memory usage.
/// </para>
/// </remarks>
public sealed class ComboBreakRecorder : IComboBreakRecorder
{
    private readonly ConcurrentQueue<ComboBreakSnapshot> _buffer = new();
    private int _count;
    private const int MaxSize = 100;
    
    /// <inheritdoc />
    public int Count => _count;
    
    /// <inheritdoc />
    public void Record(ComboBreakSnapshot snapshot)
    {
        _buffer.Enqueue(snapshot);
        var newCount = Interlocked.Increment(ref _count);
        
        // Bounded eviction using Interlocked (no lock, no Count in loop)
        while (newCount > MaxSize && _buffer.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
            newCount = _count;
        }
    }
    
    /// <inheritdoc />
    public IReadOnlyList<ComboBreakSnapshot> GetAll()
        => _buffer.ToArray();
    
    /// <inheritdoc />
    public ComboBreakSnapshot? GetByCorrelationId(string correlationId)
        => _buffer.FirstOrDefault(s => 
            s.CorrelationId.Equals(correlationId, StringComparison.OrdinalIgnoreCase));
}
