using Rgt.Space.Core.Abstractions.Debugging;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.API.Middleware;

/// <summary>
/// Middleware that attaches Combo-Break Debugger headers to error responses.
/// Only active in Development environment. Headers are added to 5xx responses only.
/// </summary>
/// <remarks>
/// <para>
/// Headers added:
/// <list type="bullet">
///   <item><c>X-Checkpoint-Last</c>: Last successfully completed checkpoint</item>
///   <item><c>X-Checkpoint-Current</c>: Checkpoint where failure occurred</item>
///   <item><c>X-Trace-Id</c>: OpenTelemetry trace ID for correlation</item>
/// </list>
/// </para>
/// <para>
/// <b>Fail-Silent:</b> All header additions are wrapped in try-catch.
/// Debug telemetry must never break the response.
/// </para>
/// </remarks>
public sealed class ComboBreakHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public ComboBreakHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only add headers in Development environment
        if (_environment.IsDevelopment())
        {
            // Use OnStarting to add headers right before the response is sent
            context.Response.OnStarting(() =>
            {
                // Fail-silent: Debug telemetry must never break the response
                try
                {
                    AddCheckpointHeaders(context);
                }
                catch
                {
                    // Swallow all exceptions - debug headers are not critical
                }
                
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }

    private static void AddCheckpointHeaders(HttpContext context)
    {
        var statusCode = context.Response.StatusCode;
        
        // Only add headers for 5xx errors (skip routine 400/404)
        if (statusCode < 500)
        {
            return;
        }
        
        // ✅ FIX: Read tracker directly (works for exceptions AND Result.Fail)
        // Previously read from Items, which was only populated by GlobalExceptionHandler (exceptions only)
        var tracker = context.RequestServices.GetService<ICheckpointTracker>();
        
        var current = tracker?.Current;  // NO coalescing — null is valuable signal
        var last = tracker?.Last;
        
        if (!string.IsNullOrEmpty(current))
        {
            context.Response.Headers[HttpConstants.Headers.CheckpointCurrent] = current;
        }
        
        if (!string.IsNullOrEmpty(last))
        {
            context.Response.Headers[HttpConstants.Headers.CheckpointLast] = last;
        }
        
        // Add trace ID for distributed tracing correlation
        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            context.Response.Headers[HttpConstants.Headers.TraceId] = traceId;
        }
    }
}

/// <summary>
/// Extension methods for registering <see cref="ComboBreakHeadersMiddleware"/>.
/// </summary>
public static class ComboBreakHeadersMiddlewareExtensions
{
    /// <summary>
    /// Adds the Combo-Break Debugger headers middleware to the pipeline.
    /// Should be added early in the pipeline (after exception handling).
    /// </summary>
    public static IApplicationBuilder UseComboBreakHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ComboBreakHeadersMiddleware>();
    }
}
