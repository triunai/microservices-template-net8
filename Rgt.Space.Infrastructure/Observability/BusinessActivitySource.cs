using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Options;
using Rgt.Space.Core.Configuration;

namespace Rgt.Space.Infrastructure.Observability;

/// <summary>
/// Provides a singleton ActivitySource for business-level tracing.
/// Follows OpenTelemetry best practices: one source per service, configurable name.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase 3 Scaffold:</b> This ActivitySource is ready for OTel integration
/// but not wired to any exporter by default. Solutions can wire it to
/// Jaeger/Tempo/AppInsights via AddSource() in their OTel configuration.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// using var activity = BusinessActivitySource.Instance.StartActivity("handler:CreateBooking");
/// // ... do work ...
/// </code>
/// When no listeners are registered, StartActivity() returns null (zero overhead).
/// </para>
/// </remarks>
public static class BusinessActivitySource
{
    private static readonly Lazy<ActivitySource> _lazySource = new(CreateActivitySource);
    private static string? _configuredName;
    
    /// <summary>
    /// The shared ActivitySource instance for business operations.
    /// </summary>
    public static ActivitySource Instance => _lazySource.Value;
    
    /// <summary>
    /// Configures the ActivitySource name before first access.
    /// Must be called during startup, before any activity is created.
    /// </summary>
    public static void Configure(ComboBreakDebuggerOptions options)
    {
        if (_lazySource.IsValueCreated)
        {
            // Already initialized — log warning but don't throw
            // (graceful degradation)
            return;
        }
        
        _configuredName = options.ActivitySourceName;
    }
    
    private static ActivitySource CreateActivitySource()
    {
        var name = _configuredName 
            ?? GetDefaultSourceName();
        
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() 
            ?? "1.0.0";
        
        return new ActivitySource(name, version);
    }
    
    private static string GetDefaultSourceName()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name 
            ?? "Rgt.Space";
        
        return $"{assemblyName}.Business";
    }
}
