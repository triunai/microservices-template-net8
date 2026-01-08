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
        // Guard against null/empty checkpoints
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            return; // Silently skip invalid checkpoints
        }
        
        // Enforce bounded nesting to prevent unbounded memory growth
        if (_stack.Count >= MaxStackDepth)
        {
            // Skip tracking deeper nesting rather than crash
            return;
        }
        
        _stack.Push(checkpoint);
        _lastUpdatedAt = DateTimeOffset.UtcNow;
        
        // Use Activity TAGS (not Events) to avoid high cardinality in traces
        Activity.Current?.SetTag("checkpoint.current", checkpoint);
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
        
        var failedAt = suffix != null 
            ? $"{Current}:{suffix}" 
            : Current;
        
        // Add failure event (events are OK for failures - low cardinality)
        Activity.Current?.AddEvent(new ActivityEvent($"failed:{failedAt}"));
        Activity.Current?.SetTag("checkpoint.broken", failedAt);
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
