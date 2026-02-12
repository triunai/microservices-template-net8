using Rgt.Space.Core.Abstractions.Debugging;

namespace Rgt.Space.Core.Debugging;

/// <summary>
/// Pure function calculator for combo positioning.
/// Determines "step X of Y" based on declared combos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step Extraction:</b> Checkpoint strings like "step:ValidateInput:exception"
/// are parsed to extract the step name "ValidateInput" for matching.
/// </para>
/// <para>
/// <b>Matching Strategy:</b> Exact match on extracted step name.
/// Suffixes like ":exception" are ignored for matching but preserved as metadata.
/// </para>
/// </remarks>
public static class ComboPositionCalculator
{
    /// <summary>
    /// Known checkpoint prefixes for step extraction.
    /// </summary>
    private static readonly string[] StepPrefixes = { "step:", "repo:", "ext:", "handler:" };
    
    /// <summary>
    /// Calculates the position within a declared combo.
    /// </summary>
    /// <param name="provider">The combo map provider</param>
    /// <param name="handlerName">The handler name (e.g., "CreateBookingCommand")</param>
    /// <param name="checkpointCurrent">Current checkpoint string (e.g., "step:ValidateInput:exception")</param>
    /// <param name="checkpointLast">Last completed checkpoint string (optional)</param>
    /// <returns>ComboPosition if combo is declared, null otherwise</returns>
    public static ComboPosition? Calculate(
        IComboMapProvider provider,
        string? handlerName,
        string? checkpointCurrent,
        string? checkpointLast)
    {
        if (string.IsNullOrEmpty(handlerName))
            return null;
        
        // Try to get the declared combo for this handler
        if (!provider.TryGetCombo(handlerName, out var declaredSteps) || declaredSteps.Count == 0)
            return null;
        
        // Extract step name from checkpoint string
        var currentStepName = ExtractStepName(checkpointCurrent);
        var lastStepName = ExtractStepName(checkpointLast);
        
        // Find position in declared combo
        var currentIndex = FindStepIndex(declaredSteps, currentStepName);
        var lastIndex = FindStepIndex(declaredSteps, lastStepName);
        
        // Determine if step is unknown (not in declared combo)
        var isUnknownStep = currentStepName != null && currentIndex < 0;
        
        // Calculate completed steps (up to and including lastIndex)
        var completedSteps = lastIndex >= 0
            ? declaredSteps.Take(lastIndex + 1).ToList()
            : new List<string>();
        
        // Calculate remaining steps (after currentIndex, or all if unknown)
        var remainingSteps = currentIndex >= 0
            ? declaredSteps.Skip(currentIndex + 1).ToList()
            : isUnknownStep
                ? new List<string>() // Unknown step - can't determine remaining
                : declaredSteps.ToList(); // No current - all steps remain
        
        // Next step (if known and not at end)
        var nextStep = remainingSteps.Count > 0 ? remainingSteps[0] : null;
        
        return new ComboPosition
        {
            IsDeclared = true,
            IsUnknownStep = isUnknownStep,
            CurrentIndex = Math.Max(currentIndex, 0),
            TotalSteps = declaredSteps.Count,
            Remaining = remainingSteps.Count,
            CurrentStep = currentStepName,
            NextStep = nextStep,
            CompletedSteps = completedSteps,
            RemainingSteps = remainingSteps,
            DeclaredSteps = declaredSteps
        };
    }
    
    /// <summary>
    /// Extracts the step name from a checkpoint string.
    /// "step:ValidateInput:exception" → "ValidateInput"
    /// "handler:CreateBooking" → "CreateBooking"
    /// </summary>
    public static string? ExtractStepName(string? checkpoint)
    {
        if (string.IsNullOrEmpty(checkpoint))
            return null;
        
        // Remove known prefixes
        var working = checkpoint;
        foreach (var prefix in StepPrefixes)
        {
            if (working.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                working = working.Substring(prefix.Length);
                break;
            }
        }
        
        // Remove suffixes (anything after first colon in remaining string)
        var colonIndex = working.IndexOf(':');
        if (colonIndex > 0)
        {
            working = working.Substring(0, colonIndex);
        }
        
        return working;
    }
    
    /// <summary>
    /// Finds the index of a step in the declared steps list (case-insensitive).
    /// </summary>
    private static int FindStepIndex(IReadOnlyList<string> declaredSteps, string? stepName)
    {
        if (string.IsNullOrEmpty(stepName))
            return -1;
        
        for (var i = 0; i < declaredSteps.Count; i++)
        {
            if (string.Equals(declaredSteps[i], stepName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        
        return -1;
    }
}
