namespace MicroservicesBase.Core.Constants;

/// <summary>
/// HTTP-related constants (status codes, RFC URIs, content types).
/// Centralizes all HTTP magic values for consistency and maintainability.
/// </summary>
public static class HttpConstants
{
    /// <summary>
    /// HTTP status codes
    /// </summary>
    public static class StatusCodes
    {
        public const int Ok = 200;
        public const int Created = 201;
        public const int NoContent = 204;
        
        public const int BadRequest = 400;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int Conflict = 409;
        public const int UnprocessableEntity = 422;
        public const int TooManyRequests = 429;
        
        public const int InternalServerError = 500;
        public const int ServiceUnavailable = 503;
    }

    /// <summary>
    /// RFC 7807 ProblemDetails type URIs, for error 505s
    /// </summary>
    public static class ProblemTypes
    {
        private const string RfcBase = "https://tools.ietf.org/html/rfc9110#section-15";
        
        public const string BadRequest = $"{RfcBase}.5.1";
        public const string Unauthorized = $"{RfcBase}.5.2";
        public const string Forbidden = $"{RfcBase}.5.4";
        public const string NotFound = $"{RfcBase}.5.5";
        public const string Conflict = $"{RfcBase}.5.10";
        public const string UnprocessableEntity = $"{RfcBase}.5.22";
        public const string TooManyRequests = "https://httpstatuses.com/429";
        public const string InternalServerError = $"{RfcBase}.6.1";
        public const string ServiceUnavailable = $"{RfcBase}.6.4";
        
        /// <summary>
        /// Custom API error type URI pattern
        /// </summary>
        public const string ApiErrorBase = "https://api.errors/";
    }

    /// <summary>
    /// HTTP header names
    /// </summary>
    public static class Headers
    {
        // Request/Response tracking
        public const string CorrelationId = "X-Correlation-Id";
        public const string RequestId = "X-Request-Id";
        public const string TraceId = "X-Trace-Id";
        
        // Multi-tenancy
        public const string Tenant = "X-Tenant";
        
        // API versioning
        public const string ApiVersion = "X-Api-Version";
        public const string ApiVersions = "X-Api-Versions";
        
        // Rate limiting
        public const string RateLimitLimit = "X-RateLimit-Limit";
        public const string RateLimitRemaining = "X-RateLimit-Remaining";
        public const string RateLimitReset = "X-RateLimit-Reset";
        public const string RateLimitWindow = "X-RateLimit-Window";
        public const string RetryAfter = "Retry-After";
    }

    /// <summary>
    /// Content-Type values
    /// </summary>
    public static class ContentTypes
    {
        public const string Json = "application/json";
        public const string ProblemJson = "application/problem+json";
        public const string Xml = "application/xml";
        public const string FormUrlEncoded = "application/x-www-form-urlencoded";
    }

    /// <summary>
    /// HttpContext.Items keys (for storing request-scoped data)
    /// </summary>
    public static class ContextKeys
    {
        public const string CorrelationId = "CorrelationId";
        public const string TenantId = "TenantId";
        public const string UserId = "UserId";
        public const string ClientId = "ClientId";
    }
}

