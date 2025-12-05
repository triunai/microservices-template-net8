using Microsoft.AspNetCore.Mvc;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.API.ProblemDetails
{
    /// <summary>
    /// Factory for creating RFC 7807 ProblemDetails responses enriched with:
    /// - Correlation ID (for request tracing)
    /// - Error code (for client-side handling)
    /// - Trace ID (for distributed tracing)
    /// </summary>
    public static class ProblemDetailsFactory
    {
        /// <summary>
        /// Creates a ProblemDetails response from an error code.
        /// </summary>
        public static Microsoft.AspNetCore.Mvc.ProblemDetails Create(
            HttpContext httpContext,
            string errorCode,
            string? detail = null,
            string? instance = null)
        {
            var statusCode = ErrorCatalog.GetStatusCode(errorCode);
            var title = ErrorCatalog.GetTitle(errorCode);
            
            var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = $"{HttpConstants.ProblemTypes.ApiErrorBase}{errorCode}",
                Title = title,
                Status = statusCode,
                Detail = detail ?? title,
                Instance = instance ?? httpContext.Request.Path
            };
            
            EnrichWithContext(problemDetails, httpContext, errorCode);
            
            return problemDetails;
        }
        
        /// <summary>
        /// Creates a ProblemDetails response from an exception.
        /// </summary>
        public static Microsoft.AspNetCore.Mvc.ProblemDetails CreateFromException(
            HttpContext httpContext,
            Exception exception,
            bool includeDetails = false)
        {
            var (errorCode, detail) = exception switch
            {
                AppException appEx => (appEx.ErrorCode, appEx.Message),
                _ => (ErrorCatalog.INTERNAL_ERROR, includeDetails ? exception.Message : "An unexpected error occurred.")
            };
            
            var statusCode = ErrorCatalog.GetStatusCode(errorCode);
            var title = ErrorCatalog.GetTitle(errorCode);
            
            var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = $"{HttpConstants.ProblemTypes.ApiErrorBase}{errorCode}",
                Title = title,
                Status = statusCode,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
            
            EnrichWithContext(problemDetails, httpContext, errorCode);
            
            // Add exception details only in development
            if (includeDetails && exception is not AppException)
            {
                problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
                problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            }
            
            return problemDetails;
        }
        
        /// <summary>
        /// Creates a ValidationProblemDetails response for validation errors.
        /// </summary>
        public static ValidationProblemDetails CreateValidation(
            HttpContext httpContext,
            Dictionary<string, string[]> errors,
            string? detail = null)
        {
            var problemDetails = new ValidationProblemDetails(errors)
            {
                Type = "https://api.errors/VALIDATION_ERROR",
                Title = "Validation Failed",
                Status = 400,
                Detail = detail ?? "One or more validation errors occurred.",
                Instance = httpContext.Request.Path
            };
            
            EnrichWithContext(problemDetails, httpContext, ErrorCatalog.VALIDATION_ERROR);
            
            return problemDetails;
        }
        
        /// <summary>
        /// Enriches ProblemDetails with correlation ID, tenant ID, trace ID, and error code.
        /// </summary>
        private static void EnrichWithContext(
            Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails,
            HttpContext httpContext,
            string errorCode)
        {
            // Add correlation ID (for request tracing)
            if (httpContext.Items.TryGetValue(HttpConstants.ContextKeys.CorrelationId, out var correlationId))
            {
                problemDetails.Extensions["correlationId"] = correlationId?.ToString();
            }
            
            // Add tenant ID (for multi-tenant context)
            if (httpContext.Items.TryGetValue(HttpConstants.ContextKeys.TenantId, out var tenantId))
            {
                problemDetails.Extensions["tenantId"] = tenantId?.ToString();
            }
            
            // Add trace ID (for distributed tracing)
            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
            
            // Add error code (for client-side handling)
            problemDetails.Extensions["errorCode"] = errorCode;
            
            // Add timestamp
            problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;
        }
    }
}

