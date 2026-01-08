namespace Rgt.Space.Core.Abstractions.Debugging;

/// <summary>
/// Tracks checkpoint progression within a request scope.
/// Used by the Combo-Break Debugger to identify the exact point of failure.
/// </summary>
/// <remarks>
/// <para>
/// This service is request-scoped. Each HTTP request gets its own tracker instance.
/// </para>
/// <para>
/// <b>Parallel Steps Constraint:</b> The stack model does NOT support parallel step tracking.
/// <c>InStep()</c> must not be used in parallel (e.g., inside Task.WhenAll).
/// </para>
/// </remarks>
public interface ICheckpointTracker
{
    /// <summary>
    /// The checkpoint currently being attempted (top of stack).
    /// On failure, this is the "break point".
    /// </summary>
    string? Current { get; }
    
    /// <summary>
    /// The last successfully completed checkpoint.
    /// This remains immutable when a failure occurs.
    /// </summary>
    string? Last { get; }
    
    /// <summary>
    /// Full history of completed checkpoints (for debugging context).
    /// Bounded to prevent unbounded memory growth.
    /// </summary>
    IReadOnlyList<string> History { get; }
    
    /// <summary>
    /// When the tracker was last updated (UTC).
    /// </summary>
    DateTimeOffset? LastUpdatedAt { get; }
    
    /// <summary>
    /// Mark entry to a checkpoint (pushes to stack).
    /// Sets <see cref="Current"/> to the checkpoint name.
    /// </summary>
    /// <param name="checkpoint">Checkpoint name (e.g., "handler:CreateOrder", "step:ValidateInput")</param>
    void Enter(string checkpoint);
    
    /// <summary>
    /// Mark successful completion of the current checkpoint.
    /// Pops from stack, adds to <see cref="History"/>, and updates <see cref="Last"/>.
    /// </summary>
    void Complete();
    
    /// <summary>
    /// Mark failure at the current checkpoint.
    /// Does NOT mutate <see cref="Last"/> — preserves last known good.
    /// Does NOT pop the stack — <see cref="Current"/> remains as the break point.
    /// </summary>
    /// <param name="suffix">Optional suffix to append (e.g., "exception", "result-failed")</param>
    void Fail(string? suffix = null);
    
    /// <summary>
    /// Execute async work within a checkpoint scope.
    /// Automatically calls Enter/Complete/Fail based on outcome.
    /// </summary>
    /// <typeparam name="T">Return type of the work</typeparam>
    /// <param name="checkpoint">Checkpoint name</param>
    /// <param name="work">Async work to execute</param>
    /// <returns>Result of the work</returns>
    Task<T> InStep<T>(string checkpoint, Func<Task<T>> work);
    
    /// <summary>
    /// Execute sync work within a checkpoint scope.
    /// Automatically calls Enter/Complete/Fail based on outcome.
    /// </summary>
    /// <typeparam name="T">Return type of the work</typeparam>
    /// <param name="checkpoint">Checkpoint name</param>
    /// <param name="work">Sync work to execute</param>
    /// <returns>Result of the work</returns>
    T InStep<T>(string checkpoint, Func<T> work);
}
