using MicroservicesBase.Core.Constants;
using Serilog.Context;

namespace MicroservicesBase.API.Middleware
{
    /// <summary>
    /// Middleware that generates or extracts a correlation ID for request tracing.
    /// The correlation ID is:
    /// 1. Read from X-Correlation-Id header (if client provides one)
    /// 2. Generated as a new GUID (if not provided)
    /// 3. Added to response headers for client tracking
    /// 4. Pushed to Serilog's LogContext for log enrichment
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Try to get correlation ID from request header, or generate a new one
            var correlationId = context.Request.Headers[HttpConstants.Headers.CorrelationId].FirstOrDefault()
                                ?? Guid.NewGuid().ToString();

            // Store in HttpContext.Items for access by other middleware/endpoints
            context.Items[HttpConstants.ContextKeys.CorrelationId] = correlationId;

            // Add to response headers so client can track the request
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(HttpConstants.Headers.CorrelationId))
                {
                    context.Response.Headers.Append(HttpConstants.Headers.CorrelationId, correlationId);
                }
                return Task.CompletedTask;
            });

            // Push to Serilog's LogContext - this enriches ALL logs in this request
            using (LogContext.PushProperty(HttpConstants.ContextKeys.CorrelationId, correlationId))
            {
                await _next(context);
            }
        }
    }
}


