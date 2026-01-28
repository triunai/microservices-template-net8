namespace Rgt.Space.Core.Debugging;

/// <summary>
/// Represents the position within a declared combo (step list).
/// Used by Phase 3 to provide "step X of Y" information.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nullability Semantics:</b>
/// - Null ComboPosition = no combo declared for this handler
/// - Non-null with IsDeclared=true = combo declared
/// - IsUnknownStep=true = step not in declared combo (dev may have added step but forgot ComboMap)
/// </para>
/// </remarks>
public sealed record ComboPosition
{
    /// <summary>
    /// True if a combo was declared for this handler.
    /// </summary>
    public required bool IsDeclared { get; init; }
    
    /// <summary>
    /// True if the current step was not found in the declared combo.
    /// Indicates a developer added a new step but forgot to update ComboMap.
    /// </summary>
    public bool IsUnknownStep { get; init; }
    
    /// <summary>
    /// Zero-based index of the current step in the combo.
    /// -1 if unknown step or no current step.
    /// </summary>
    public required int CurrentIndex { get; init; }
    
    /// <summary>
    /// Total number of steps in the declared combo.
    /// </summary>
    public required int TotalSteps { get; init; }
    
    /// <summary>
    /// Number of steps remaining after the current step.
    /// </summary>
    public required int Remaining { get; init; }
    
    /// <summary>
    /// The extracted step name (without prefix/suffix).
    /// </summary>
    public string? CurrentStep { get; init; }
    
    /// <summary>
    /// Name of the next step (null if at end of combo or unknown step).
    /// </summary>
    public string? NextStep { get; init; }
    
    /// <summary>
    /// Steps that were completed before the failure.
    /// </summary>
    public required IReadOnlyList<string> CompletedSteps { get; init; }
    
    /// <summary>
    /// Steps that would have executed after the failure point.
    /// </summary>
    public required IReadOnlyList<string> RemainingSteps { get; init; }
    
    /// <summary>
    /// The full declared combo (all steps). Only included when requested.
    /// </summary>
    public IReadOnlyList<string>? DeclaredSteps { get; init; }
    
    /// <summary>
    /// Human-readable summary: "Step 2/5, 3 remaining, next: SaveToDb"
    /// </summary>
    public override string ToString()
    {
        if (IsUnknownStep)
            return $"Unknown step '{CurrentStep}' (not in declared combo of {TotalSteps} steps)";
        
        return $"Step {CurrentIndex + 1}/{TotalSteps}, {Remaining} remaining, next: {NextStep ?? "(done)"}";
    }
}
