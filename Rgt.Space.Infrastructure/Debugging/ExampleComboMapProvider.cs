using Rgt.Space.Core.Abstractions.Debugging;

namespace Rgt.Space.Infrastructure.Debugging;

/// <summary>
/// Example combo map provider demonstrating how solutions define their combos.
/// </summary>
/// <remarks>
/// <para>
/// <b>To enable Phase 3 for your solution:</b>
/// 1. Create your own AppComboMapProvider with your handler combos
/// 2. Register it in DI: services.AddSingleton&lt;IComboMapProvider, YourAppComboMapProvider&gt;();
/// 3. Ensure your checkpoints follow the step:{StepName} convention
/// </para>
/// <para>
/// <b>Combo Declaration Guidelines:</b>
/// - Only declare combos for critical flows (not every handler)
/// - Use exact step names that match your InStep() calls
/// - Keep combos updated when you add/remove steps
/// </para>
/// </remarks>
/// <example>
/// Example combo declaration:
/// <code>
/// private static readonly Dictionary&lt;string, IReadOnlyList&lt;string&gt;&gt; _combos = new()
/// {
///     ["CreateBookingCommand"] = new[] { "ValidateInput", "LoadEntity", "CheckPermission", "SaveToDb", "PublishEvent" },
///     ["ProcessPaymentCommand"] = new[] { "ValidateAmount", "ReserveInventory", "ChargeCard", "ConfirmOrder" },
/// };
/// </code>
/// </example>
public sealed class ExampleComboMapProvider : IComboMapProvider
{
    /// <summary>
    /// Declared combos for this solution.
    /// Key: Handler name (e.g., "CreateBookingCommand")
    /// Value: Ordered list of step names (without prefix)
    /// </summary>
    private static readonly Dictionary<string, IReadOnlyList<string>> _combos = new(StringComparer.OrdinalIgnoreCase)
    {
        // Example: ComboBreakTest handler from the debugging endpoint
        ["Query"] = new[] { "ValidateInput", "FetchData", "ProcessData", "SaveResult" },
        
        // Add your solution's critical flows here:
        // ["CreateBookingCommand"] = new[] { "ValidateInput", "LoadEntity", "CheckPermission", "SaveToDb", "PublishEvent" },
    };
    
    private static readonly IReadOnlyList<string> EmptySteps = Array.Empty<string>();
    
    /// <inheritdoc />
    public bool TryGetCombo(string handlerName, out IReadOnlyList<string> steps)
    {
        if (_combos.TryGetValue(handlerName, out var combo))
        {
            steps = combo;
            return true;
        }
        
        steps = EmptySteps;
        return false;
    }
}
