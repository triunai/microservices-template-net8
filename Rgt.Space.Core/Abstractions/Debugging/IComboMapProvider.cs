namespace Rgt.Space.Core.Abstractions.Debugging;

/// <summary>
/// Provides combo (step list) declarations for handlers.
/// Used by Phase 3 to compute "step X of Y" positioning.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cross-Solution Pattern:</b> Each solution implements its own provider
/// with handler-specific combo declarations. The template ships with
/// <see cref="NullComboMapProvider"/> which declares no combos.
/// </para>
/// <para>
/// <b>Opt-In:</b> Only critical handlers should have combos declared.
/// Most handlers don't need "step X of Y" — Phase 1 tracking is sufficient.
/// </para>
/// </remarks>
public interface IComboMapProvider
{
    /// <summary>
    /// Attempts to get the declared combo (step list) for a handler.
    /// </summary>
    /// <param name="handlerName">Handler name (e.g., "CreateBookingCommand")</param>
    /// <param name="steps">The declared steps if found</param>
    /// <returns>True if a combo is declared for this handler</returns>
    bool TryGetCombo(string handlerName, out IReadOnlyList<string> steps);
}
