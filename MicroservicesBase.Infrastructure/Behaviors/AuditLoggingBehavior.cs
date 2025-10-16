using MediatR;
using MicroservicesBase.Core.Abstractions.Auditing;
using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Core.Configuration;
using MicroservicesBase.Core.Constants;
using MicroservicesBase.Core.Domain.Auditing;
using MicroservicesBase.Infrastructure.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace MicroservicesBase.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically audits all queries and commands.
/// Captures: who, what, when, where, result, duration.
/// </summary>
public sealed class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditLogger _auditLogger;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditSettings _settings;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(
        IAuditLogger auditLogger,
        ITenantProvider tenantProvider,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AuditSettings> settings,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _auditLogger = auditLogger;
        _tenantProvider = tenantProvider;
        _httpContextAccessor = httpContextAccessor;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip if audit logging is disabled
        if (!_settings.Enabled)
            return await next();

        // Apply sampling for read operations
        if (IsReadOperation(typeof(TRequest).Name) && !ShouldSampleRead())
            return await next();

        var stopwatch = Stopwatch.StartNew();
        var httpContext = _httpContextAccessor.HttpContext;
        
        // Capture context before execution
        var auditEntry = new AuditEntry
        {
            TenantId = _tenantProvider.Id ?? "Unknown",
            UserId = httpContext?.User?.Identity?.Name, // Will be populated from JWT in future
            IpAddress = GetClientIpAddress(httpContext),
            UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
            Action = DetermineAction(typeof(TRequest).Name),
            EntityType = ExtractEntityType(typeof(TRequest).Name),
            EntityId = ExtractEntityId(request),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = httpContext?.Items["CorrelationId"]?.ToString(),
            RequestPath = httpContext?.Request.Path.ToString(),
            Source = "API",
            IsSuccess = false, // Will be updated after execution
            RequestData = _settings.Payloads.LogRequests 
                ? PayloadProcessor.ProcessPayload(request, _settings.Payloads) 
                : null
        };

        TResponse? response = default;
        Exception? exception = null;

        try
        {
            // Execute the handler
            response = await next();

            // Check if response is a FluentResults Result<T> and handle failures
            var (isSuccess, statusCode, errorCode, errorMessage) = AnalyzeResponse(response);
            
            // Capture result (success or FluentResults failure) - ALWAYS capture response data for both success and failure
            auditEntry = auditEntry with
            {
                IsSuccess = isSuccess,
                StatusCode = statusCode,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                ResponseData = _settings.Payloads.LogResponses 
                    ? PayloadProcessor.ProcessPayload(BuildSerializableResponse(response, httpContext), _settings.Payloads) 
                    : null
            };
        }
        catch (Exception ex)
        {
            exception = ex;
            
            // Capture failure - also capture response data if available
            auditEntry = auditEntry with
            {
                IsSuccess = false,
                StatusCode = DetermineStatusCodeFromException(ex),
                ErrorCode = ExtractErrorCode(ex),
                ErrorMessage = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                ResponseData = _settings.Payloads.LogResponses 
                    ? PayloadProcessor.ProcessPayload(BuildExceptionResponse(ex, httpContext), _settings.Payloads) 
                    : null
            };

            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Log audit entry asynchronously (fire-and-forget via channel)
            try
            {
                await _auditLogger.LogAsync(auditEntry, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue audit entry for {Action}", auditEntry.Action);
            }
        }

        return response!;
    }

    /// <summary>
    /// Analyze response to detect FluentResults failures and determine audit details
    /// </summary>
    private static (bool IsSuccess, int StatusCode, string? ErrorCode, string? ErrorMessage) AnalyzeResponse<T>(T response)
    {
        // Check if response is a FluentResults Result<T>
        if (response is FluentResults.ResultBase result)
        {
            if (result.IsFailed)
            {
                // Extract first error for audit logging
                var firstError = result.Errors.FirstOrDefault();
                var errorCode = firstError?.Message ?? "UNKNOWN_ERROR";
                
                // Map FluentResults error codes to HTTP status codes
                var statusCode = errorCode switch
                {
                    "SALE_NOT_FOUND" => HttpConstants.StatusCodes.NotFound,
                    "VALIDATION_ERROR" => HttpConstants.StatusCodes.BadRequest,
                    "TENANT_NOT_FOUND" => HttpConstants.StatusCodes.NotFound,
                    "UNAUTHORIZED" => HttpConstants.StatusCodes.Unauthorized,
                    "FORBIDDEN" => HttpConstants.StatusCodes.Forbidden,
                    _ => HttpConstants.StatusCodes.InternalServerError
                };
                
                return (false, statusCode, errorCode, firstError?.Message);
            }
            
            // Success case
            return (true, HttpConstants.StatusCodes.Ok, null, null);
        }
        
        // Non-FluentResults response - assume success
        return (true, HttpConstants.StatusCodes.Ok, null, null);
    }

    /// <summary>
    /// Build a safe, serializable object for ResponseData.
    /// - For Result<T> success: returns the Value
    /// - For Result<T> failure: returns an enriched error shape with context (correlationId, tenantId, etc.)
    /// - Otherwise: returns the original response
    /// </summary>
    private static object? BuildSerializableResponse<T>(T response, HttpContext? httpContext)
    {
        if (response is FluentResults.ResultBase result)
        {
            if (result.IsFailed)
            {
                var errors = result.Errors?.Select(e => e.Message).Where(m => !string.IsNullOrWhiteSpace(m)).ToArray() ?? Array.Empty<string>();
                var reasons = result.Reasons?.Select(r => r.Message).Where(m => !string.IsNullOrWhiteSpace(m)).ToArray() ?? Array.Empty<string>();
                
                // Get the first error for error code
                var firstError = result.Errors?.FirstOrDefault();
                var errorCode = firstError?.Message ?? "UNKNOWN_ERROR";
                
                // Enrich with HTTP context (same as ProblemDetails)
                var enrichedResponse = new
                {
                    isFailed = true,
                    isSuccess = false,
                    errors,
                    reasons,
                    errorCode,
                    statusCode = DetermineStatusCodeFromErrorCode(errorCode),
                    timestamp = DateTimeOffset.UtcNow
                };

                // Add HTTP context if available
                if (httpContext != null)
                {
                    var correlationId = httpContext.Items.TryGetValue(HttpConstants.ContextKeys.CorrelationId, out var corrId) ? corrId?.ToString() : null;
                    var tenantId = httpContext.Items.TryGetValue(HttpConstants.ContextKeys.TenantId, out var tenant) ? tenant?.ToString() : null;
                    var traceId = httpContext.TraceIdentifier;
                    var instance = httpContext.Request.Path.ToString();

                    return new
                    {
                        enrichedResponse.isFailed,
                        enrichedResponse.isSuccess,
                        enrichedResponse.errors,
                        enrichedResponse.reasons,
                        enrichedResponse.errorCode,
                        enrichedResponse.statusCode,
                        enrichedResponse.timestamp,
                        correlationId,
                        tenantId,
                        traceId,
                        instance
                    };
                }

                return enrichedResponse;
            }

            // Success: try to extract Value via reflection for Result<T>
            var valueProp = result.GetType().GetProperty("Value");
            if (valueProp != null)
            {
                try
                {
                    return valueProp.GetValue(result);
                }
                catch
                {
                    // fall through
                }
            }

            // If we can't extract Value, serialize a minimal success shape
            return new { isFailed = false, isSuccess = true };
        }

        return response;
    }

    /// <summary>
    /// Build a safe, serializable object for ResponseData from exceptions.
    /// Creates a structured error response that can be audited.
    /// </summary>
    private static object BuildExceptionResponse(Exception exception, HttpContext? httpContext)
    {
        var errorCode = ExtractErrorCode(exception);
        var enrichedResponse = new
        {
            isFailed = true,
            isSuccess = false,
            exceptionType = exception.GetType().Name,
            message = exception.Message,
            errorCode,
            statusCode = DetermineStatusCodeFromException(exception),
            timestamp = DateTimeOffset.UtcNow,
            stackTrace = exception.StackTrace
        };

        // Add HTTP context if available
        if (httpContext != null)
        {
            var correlationId = httpContext.Items.TryGetValue(HttpConstants.ContextKeys.CorrelationId, out var corrId) ? corrId?.ToString() : null;
            var tenantId = httpContext.Items.TryGetValue(HttpConstants.ContextKeys.TenantId, out var tenant) ? tenant?.ToString() : null;
            var traceId = httpContext.TraceIdentifier;
            var instance = httpContext.Request.Path.ToString();

            return new
            {
                enrichedResponse.isFailed,
                enrichedResponse.isSuccess,
                enrichedResponse.exceptionType,
                enrichedResponse.message,
                enrichedResponse.errorCode,
                enrichedResponse.statusCode,
                enrichedResponse.timestamp,
                enrichedResponse.stackTrace,
                correlationId,
                tenantId,
                traceId,
                instance
            };
        }

        return enrichedResponse;
    }

    /// <summary>
    /// Determine if this is a read operation based on request name
    /// </summary>
    private static bool IsReadOperation(string requestName)
    {
        return requestName.Contains("Query", StringComparison.OrdinalIgnoreCase) ||
               requestName.Contains("Get", StringComparison.OrdinalIgnoreCase) ||
               requestName.Contains("Read", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Apply sampling for read operations
    /// </summary>
    private bool ShouldSampleRead()
    {
        var random = Random.Shared.Next(1, 101); // 1-100
        return random <= _settings.Sampling.ReadsPercent;
    }

    /// <summary>
    /// Determine action taxonomy from request name (e.g., "GetSaleByIdQuery" → "Sales.Read")
    /// </summary>
    private static string DetermineAction(string requestName)
    {
        // Extract entity and verb from request name
        // Examples:
        // - GetSaleByIdQuery → Sales.Read
        // - CreateSaleCommand → Sales.Create
        // - VoidSaleCommand → Sales.Void

        var name = requestName.Replace("Query", "").Replace("Command", "");

        if (name.StartsWith("Get") || name.Contains("Read"))
            return $"{ExtractEntityType(name)}.Read";
        if (name.StartsWith("Create"))
            return $"{ExtractEntityType(name)}.Create";
        if (name.StartsWith("Update"))
            return $"{ExtractEntityType(name)}.Update";
        if (name.StartsWith("Delete"))
            return $"{ExtractEntityType(name)}.Delete";
        if (name.Contains("Void"))
            return $"{ExtractEntityType(name)}.Void";
        if (name.Contains("Refund"))
            return $"{ExtractEntityType(name)}.Refund";

        return name; // Fallback
    }

    /// <summary>
    /// Extract entity type from request name (e.g., "GetSaleByIdQuery" → "Sale")
    /// </summary>
    private static string? ExtractEntityType(string requestName)
    {
        // Simple heuristic: find first capital letter sequence
        var match = System.Text.RegularExpressions.Regex.Match(requestName, @"(?:Get|Create|Update|Delete|Void|Refund)([A-Z][a-z]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extract entity ID from request (if it has an Id property)
    /// </summary>
    private static string? ExtractEntityId(TRequest request)
    {
        var idProperty = typeof(TRequest).GetProperty("Id") ?? 
                         typeof(TRequest).GetProperty("SaleId") ??
                         typeof(TRequest).GetProperty("EntityId");

        return idProperty?.GetValue(request)?.ToString();
    }

    /// <summary>
    /// Get client IP address (X-Forwarded-For aware)
    /// </summary>
    private static string? GetClientIpAddress(HttpContext? context)
    {
        if (context == null)
            return null;

        // Check X-Forwarded-For header first (for proxies/load balancers)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            return ips[0].Trim(); // First IP is the original client
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Determine HTTP status code from exception using centralized error handling.
    /// Uses ErrorCatalog.GetStatusCode() for AppException types, falls back to HttpConstants for generic exceptions.
    /// </summary>
    private static int DetermineStatusCodeFromException(Exception exception)
    {
        // Use centralized error handling for custom exceptions
        if (exception is MicroservicesBase.Core.Errors.AppException appEx)
        {
            return MicroservicesBase.Core.Errors.ErrorCatalog.GetStatusCode(appEx.ErrorCode);
        }
        
        // Fallback for generic .NET exceptions using HttpConstants
        return exception switch
        {
            ArgumentException => HttpConstants.StatusCodes.BadRequest,
            UnauthorizedAccessException => HttpConstants.StatusCodes.Unauthorized,
            InvalidOperationException =>    HttpConstants.StatusCodes.NotFound,
            NotImplementedException => HttpConstants.StatusCodes.ServiceUnavailable,
            TimeoutException => HttpConstants.StatusCodes.ServiceUnavailable,
            _ => HttpConstants.StatusCodes.InternalServerError
        };
    }

    /// <summary>
    /// Extract error code from exception using centralized error handling.
    /// Returns ErrorCode for AppException types, null for generic exceptions.
    /// </summary>
    private static string? ExtractErrorCode(Exception exception)
    {
        // Use centralized error handling for custom exceptions
        if (exception is MicroservicesBase.Core.Errors.AppException appEx)
        {
            return appEx.ErrorCode;
        }
        
        // Generic exceptions don't have error codes
        return null;
    }

    /// <summary>
    /// Determine HTTP status code from error code (same logic as ErrorCatalog)
    /// </summary>
    private static int DetermineStatusCodeFromErrorCode(string errorCode)
    {
        // Use the same logic as ErrorCatalog.GetStatusCode
        return errorCode switch
        {
            "SALE_NOT_FOUND" => MicroservicesBase.Core.Constants.HttpConstants.StatusCodes.NotFound,
            "VALIDATION_ERROR" => MicroservicesBase.Core.Constants.HttpConstants.StatusCodes.BadRequest,
            "TENANT_NOT_FOUND" => MicroservicesBase.Core.Constants.HttpConstants.StatusCodes.NotFound,
            "UNAUTHORIZED" => MicroservicesBase.Core.Constants.HttpConstants.StatusCodes.Unauthorized,
            "FORBIDDEN" => MicroservicesBase.Core.Constants.HttpConstants.StatusCodes.Forbidden,
            _ => MicroservicesBase.Core.Constants.HttpConstants.StatusCodes.InternalServerError
        };
    }
}

