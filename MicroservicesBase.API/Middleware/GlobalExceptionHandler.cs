using Microsoft.AspNetCore.Diagnostics;
using MicroservicesBase.Core.Errors;

namespace MicroservicesBase.API.Middleware
{
    /// <summary>
    /// Global exception handler using .NET 8's IExceptionHandler interface.
    /// Catches all unhandled exceptions and converts them to RFC 7807 ProblemDetails responses.
    /// Logs exceptions with Serilog including correlation ID and tenant context.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // Log the exception with full context
            LogException(httpContext, exception);

            // Create ProblemDetails response
            var includeDetails = _environment.IsDevelopment();
            var problemDetails = API.ProblemDetails.ProblemDetailsFactory.CreateFromException(
                httpContext,
                exception,
                includeDetails);

            // Set response status code
            httpContext.Response.StatusCode = problemDetails.Status ?? 500;

            // Write ProblemDetails as JSON response
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            // Return true to indicate the exception was handled
            return true;
        }

        private void LogException(HttpContext httpContext, Exception exception)
        {
            var correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "Unknown";
            var tenantId = httpContext.Items["TenantId"]?.ToString() ?? "Unknown";
            var userId = httpContext.User?.Identity?.Name ?? "Anonymous";

            // Different log levels based on exception type
            switch (exception)
            {
                case AppException appEx:
                    // Application exceptions are expected business errors (log as warning)
                    _logger.LogWarning(exception,
                        "Application error: {ErrorCode} | CorrelationId: {CorrelationId} | TenantId: {TenantId} | UserId: {UserId} | Path: {Path}",
                        appEx.ErrorCode, correlationId, tenantId, userId, httpContext.Request.Path);
                    break;

                case OperationCanceledException:
                    // Request was cancelled by client (log as information)
                    _logger.LogInformation(
                        "Request cancelled: CorrelationId: {CorrelationId} | TenantId: {TenantId} | Path: {Path}",
                        correlationId, tenantId, httpContext.Request.Path);
                    break;

                default:
                    // Unexpected exceptions are errors (log as error)
                    _logger.LogError(exception,
                        "Unhandled exception: {ExceptionType} | CorrelationId: {CorrelationId} | TenantId: {TenantId} | UserId: {UserId} | Path: {Path}",
                        exception.GetType().Name, correlationId, tenantId, userId, httpContext.Request.Path);
                    break;
            }
        }
    }
}

