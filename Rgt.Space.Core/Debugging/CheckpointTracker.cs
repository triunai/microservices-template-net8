using System.Diagnostics;
using Rgt.Space.Core.Abstractions.Debugging;

namespace Rgt.Space.Core.Debugging;

/// <summary>
/// Request-scoped checkpoint tracker for the Combo-Break Debugger.
/// Tracks the progression of checkpoints within a single request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This class is NOT thread-safe. It is designed for single-threaded,
/// sequential execution within the ASP.NET Core request pipeline.
/// </para>
/// <para>
/// <b>Parallel Steps:</b> Do not use <see cref="InStep{T}(string, Func{Task{T}})"/> inside
/// Task.WhenAll or similar parallel constructs. The stack model cannot represent parallel work.
/// </para>
/// </remarks>
public sealed class CheckpointTracker : ICheckpointTracker
{
    private readonly Stack<string> _stack = new();
    private readonly List<string> _history = new(capacity: 16);
    private string? _last;
    private DateTimeOffset? _lastUpdatedAt;
    
    private const int MaxHistorySize = 20;
    private const int MaxStackDepth = 8;

    /// <inheritdoc />
    public string? Current => _stack.Count > 0 ? _stack.Peek() : null;
    
    /// <inheritdoc />
    public string? Last => _last;
    
    /// <inheritdoc />
    public IReadOnlyList<string> History => _history;
    
    /// <inheritdoc />
    public DateTimeOffset? LastUpdatedAt => _lastUpdatedAt;

    /// <inheritdoc />
    public void Enter(string checkpoint)
    {
        // Normalize empty/null to a low-cardinality placeholder instead of skipping
        // ✅ CRITICAL: Never skip push — InStep() will always call Complete()/Fail()
        // If we skip push but Complete() still pops, we corrupt the stack!
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            checkpoint = "checkpoint:(empty)";
        }
        
        // ✅ ALWAYS push to preserve stack balance (never no-op)
        _stack.Push(checkpoint);
        _lastUpdatedAt = DateTimeOffset.UtcNow;
        
        // Only set Activity tag if within display limit (telemetry bounded, stack is not)
        if (_stack.Count <= MaxStackDepth)
        {
            Activity.Current?.SetTag("checkpoint.current", checkpoint);
        }
        else
        {
            // Signal overflow but DON'T corrupt stack — push already happened
            Activity.Current?.SetTag("checkpoint.overflow", true);
        }
    }

    /// <inheritdoc />
    public void Complete()
    {
        if (_stack.Count == 0) return;
        
        var completed = _stack.Pop();
        _last = completed;
        _lastUpdatedAt = DateTimeOffset.UtcNow;
        
        // Add to history (bounded)
        _history.Add(completed);
        if (_history.Count > MaxHistorySize)
        {
            _history.RemoveAt(0);
        }
        
        // Update Activity tags
        Activity.Current?.SetTag("checkpoint.last", _last);
        Activity.Current?.SetTag("checkpoint.current", Current); // May be null or parent
    }

    /// <inheritdoc />
    public void Fail(string? suffix = null)
    {
        // CRITICAL: DO NOT pop stack — Current IS the break point
        // CRITICAL: DO NOT overwrite _last — preserve last known good
        
        _lastUpdatedAt = DateTimeOffset.UtcNow;
        
        // Null-safe formatting: avoid ":exception" when Current is null
        var failedAt = Current switch
        {
            null when suffix != null => suffix,             // null + suffix → just suffix
            null => null,                                   // null + no suffix → null
            _ when suffix != null => $"{Current}:{suffix}", // "step:X:exception"
            _ => Current                                    // just "step:X"
        };
        
        // Add failure event (events are OK for failures - low cardinality)
        if (!string.IsNullOrEmpty(failedAt))
        {
            Activity.Current?.AddEvent(new ActivityEvent($"failed:{failedAt}"));
            Activity.Current?.SetTag("checkpoint.broken", failedAt);
        }
    }

    /// <inheritdoc />
    public async Task<T> InStep<T>(string checkpoint, Func<Task<T>> work)
    {
        Enter(checkpoint);
        try
        {
            var result = await work();
            Complete();
            return result;
        }
        catch
        {
            Fail("exception");
            throw;
        }
    }

    /// <inheritdoc />
    public T InStep<T>(string checkpoint, Func<T> work)
    {
        Enter(checkpoint);
        try
        {
            var result = work();
            Complete();
            return result;
        }
        catch
        {
            Fail("exception");
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task InStepAsync(string checkpoint, Func<Task> work)
    {
        Enter(checkpoint);
        try
        {
            await work();
            Complete();
        }
        catch
        {
            Fail("exception");
            throw;
        }
    }
    
    /// <inheritdoc />
    public void InStep(string checkpoint, Action work)
    {
        Enter(checkpoint);
        try
        {
            work();
            Complete();
        }
        catch
        {
            Fail("exception");
            throw;
        }
    }
}
