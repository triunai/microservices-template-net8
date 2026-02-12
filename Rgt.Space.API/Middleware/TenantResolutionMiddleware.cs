using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Constants;
using Rgt.Space.Infrastructure.Tenancy;
using Serilog.Context;

namespace Rgt.Space.API.Middleware
{
    /// <summary>
    /// Middleware that resolves the tenant context for the current request.
    /// Runs AFTER UseAuthentication() so JWT claims are available.
    ///
    /// Priority order:
    /// 1. JWT "tid" claim (authoritative for authenticated requests)
    /// 2. X-Tenant header (fallback for unauthenticated/health endpoints)
    ///
    /// If both JWT tid and X-Tenant header are present and they don't match,
    /// the request is rejected with 403 (tenant spoofing attempt).
    /// </summary>
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantResolutionMiddleware> _logger;
        private const string UnknownTenant = "Unknown";

        public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
        {
            string? tenantCode = null;
            string? source = null;

            // Priority 1: JWT "tid" claim (authoritative for authenticated users)
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var jwtTid = context.User.FindFirst("tid")?.Value;
                var headerTenant = context.Request.Headers[HttpConstants.Headers.Tenant].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(jwtTid))
                {
                    // JWT tid is authoritative — validate header doesn't conflict (case-insensitive)
                    if (!string.IsNullOrWhiteSpace(headerTenant) &&
                        !string.Equals(headerTenant, jwtTid, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Tenant mismatch: JWT tid={JwtTid}, X-Tenant={HeaderTenant}. Rejecting request.",
                            jwtTid, headerTenant);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }

                    tenantCode = jwtTid.ToUpperInvariant();
                    source = "JWT-tid-claim";
                    _logger.LogDebug("Tenant resolved from JWT tid claim: {TenantCode}", tenantCode);
                }
            }

            // Priority 2: X-Tenant header (fallback for unauthenticated requests)
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                tenantCode = context.Request.Headers[HttpConstants.Headers.Tenant].FirstOrDefault()?.ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    source = "X-Tenant-header";
                    _logger.LogDebug("Tenant resolved from X-Tenant header: {TenantCode}", tenantCode);
                }
            }

            // Set tenant context
            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                if (tenantProvider is HeaderTenantProvider concrete)
                {
                    concrete.SetTenant(tenantCode);
                }

                context.Items[HttpConstants.ContextKeys.TenantId] = tenantCode;
                _logger.LogInformation("Tenant resolved: {TenantCode} (source: {Source})", tenantCode, source);
            }
            else
            {
                context.Items[HttpConstants.ContextKeys.TenantId] = UnknownTenant;

                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    _logger.LogWarning(
                        "Authenticated request to {Path} has no tenant context (no tid claim or X-Tenant header)",
                        context.Request.Path);
                }
                else
                {
                    _logger.LogDebug("Unauthenticated request to {Path} has no tenant context",
                        context.Request.Path);
                }
            }

            // Push to Serilog's LogContext and continue pipeline
            using (LogContext.PushProperty(HttpConstants.ContextKeys.TenantId,
                context.Items[HttpConstants.ContextKeys.TenantId]?.ToString() ?? UnknownTenant))
            {
                await _next(context);
            }
        }
    }
}
