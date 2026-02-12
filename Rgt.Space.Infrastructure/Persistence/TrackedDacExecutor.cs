using System.Diagnostics;
using Rgt.Space.Core.Abstractions.Debugging;

namespace Rgt.Space.Infrastructure.Persistence;

/// <summary>
/// Utility for wrapping DAC/Repository methods with checkpoint tracking.
/// Provides "repo:{DacName}:{MethodName}" checkpoints for the Combo-Break Debugger.
/// </summary>
/// <remarks>
/// <para>
/// <b>Usage:</b> Inject into DACs and wrap method bodies with <c>ExecuteAsync</c>.
/// Use <c>nameof()</c> for both dacName and methodName to ensure low-cardinality.
/// </para>
/// <para>
/// <b>Parallel Guardrail:</b> If <see cref="ICheckpointTracker.Current"/> already starts with "repo:",
/// tracking is skipped to mitigate common <c>Task.WhenAll</c> overlaps. This is timing-dependent,
/// not a perfect detector — but it prevents stack corruption in common parallel patterns.
/// </para>
/// </remarks>
public sealed class TrackedDacExecutor
{
    private readonly ICheckpointTracker _tracker;
    private const string RepoPrefix = "repo:";

    public TrackedDacExecutor(ICheckpointTracker tracker)
    {
        _tracker = tracker;
    }

    /// <summary>
    /// Executes async work with checkpoint tracking. Returns a value.
    /// </summary>
    /// <typeparam name="T">Return type of the work.</typeparam>
    /// <param name="dacName">DAC class name (use <c>nameof(ClientWriteDac)</c>).</param>
    /// <param name="methodName">Method name (use <c>nameof(CreateAsync)</c>).</param>
    /// <param name="work">The async work to execute.</param>
    /// <returns>Result of the work.</returns>
    public async Task<T> ExecuteAsync<T>(string dacName, string methodName, Func<Task<T>> work)
    {
        if (ShouldSkipTracking())
        {
            return await work();
        }

        var checkpoint = $"{RepoPrefix}{dacName}:{methodName}";
        return await _tracker.InStep(checkpoint, work);
    }

    /// <summary>
    /// Executes async work with checkpoint tracking. Void return.
    /// </summary>
    /// <param name="dacName">DAC class name (use <c>nameof(ClientWriteDac)</c>).</param>
    /// <param name="methodName">Method name (use <c>nameof(CreateAsync)</c>).</param>
    /// <param name="work">The async work to execute.</param>
    public async Task ExecuteAsync(string dacName, string methodName, Func<Task> work)
    {
        if (ShouldSkipTracking())
        {
            await work();
            return;
        }

        var checkpoint = $"{RepoPrefix}{dacName}:{methodName}";
        await _tracker.InStepAsync(checkpoint, work);
    }

    /// <summary>
    /// Executes sync work with checkpoint tracking. Returns a value.
    /// </summary>
    /// <typeparam name="T">Return type of the work.</typeparam>
    /// <param name="dacName">DAC class name (use <c>nameof(ClientWriteDac)</c>).</param>
    /// <param name="methodName">Method name (use <c>nameof(CreateAsync)</c>).</param>
    /// <param name="work">The sync work to execute.</param>
    /// <returns>Result of the work.</returns>
    public T Execute<T>(string dacName, string methodName, Func<T> work)
    {
        if (ShouldSkipTracking())
        {
            return work();
        }

        var checkpoint = $"{RepoPrefix}{dacName}:{methodName}";
        return _tracker.InStep(checkpoint, work);
    }

    /// <summary>
    /// Executes sync work with checkpoint tracking. Void return.
    /// </summary>
    /// <param name="dacName">DAC class name (use <c>nameof(ClientWriteDac)</c>).</param>
    /// <param name="methodName">Method name (use <c>nameof(CreateAsync)</c>).</param>
    /// <param name="work">The sync work to execute.</param>
    public void Execute(string dacName, string methodName, Action work)
    {
        if (ShouldSkipTracking())
        {
            work();
            return;
        }

        var checkpoint = $"{RepoPrefix}{dacName}:{methodName}";
        _tracker.InStep(checkpoint, work);
    }

    /// <summary>
    /// Returns true if we should skip tracking (already inside a repo checkpoint).
    /// Mitigates common parallel overlaps by detecting repo-inside-repo.
    /// </summary>
    private bool ShouldSkipTracking()
    {
        var current = _tracker.Current;
        if (current?.StartsWith(RepoPrefix, StringComparison.Ordinal) == true)
        {
            // Tag telemetry to signal this happened (dev visibility)
            Activity.Current?.SetTag("checkpoint.parallel_skip", true);
            return true;
        }
        return false;
    }
}
