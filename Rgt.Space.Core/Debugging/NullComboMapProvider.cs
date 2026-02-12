using Rgt.Space.Core.Abstractions.Debugging;

namespace Rgt.Space.Core.Debugging;

/// <summary>
/// Default combo map provider that declares no combos.
/// Used when Phase 3 is not enabled for a solution.
/// </summary>
/// <remarks>
/// To enable Phase 3 "step X of Y" tracking, solutions should implement
/// their own <see cref="IComboMapProvider"/> and register it in DI.
/// </remarks>
public sealed class NullComboMapProvider : IComboMapProvider
{
    private static readonly IReadOnlyList<string> EmptySteps = Array.Empty<string>();
    
    /// <inheritdoc />
    public bool TryGetCombo(string handlerName, out IReadOnlyList<string> steps)
    {
        steps = EmptySteps;
        return false;
    }
}
