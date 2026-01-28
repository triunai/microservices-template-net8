namespace Rgt.Space.Core.Configuration;

/// <summary>
/// Configuration options for the Combo-Break Debugger feature.
/// </summary>
public sealed class ComboBreakDebuggerOptions
{
    public const string SectionName = "ComboBreakDebugger";
    
    /// <summary>
    /// Enable the Combo Meter (step X of Y positioning).
    /// Requires an IComboMapProvider with declared combos.
    /// Default: false
    /// </summary>
    public bool EnableComboMeter { get; set; } = false;
    
    /// <summary>
    /// Enable business-level Activity spans for handlers.
    /// Creates child spans for each MediatR handler execution.
    /// Default: false (uses tags only)
    /// </summary>
    public bool EnableBusinessSpans { get; set; } = false;
    
    /// <summary>
    /// Custom ActivitySource name override.
    /// Default: "{EntryAssembly}.Business"
    /// </summary>
    public string? ActivitySourceName { get; set; }
    
    /// <summary>
    /// Maximum combo breaks to keep in memory (dev recorder).
    /// Default: 100
    /// </summary>
    public int MaxRecorderSize { get; set; } = 100;
}
