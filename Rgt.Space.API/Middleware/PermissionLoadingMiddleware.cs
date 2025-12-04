using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Rgt.Space.Core.Abstractions.Identity;

namespace Rgt.Space.API.Middleware;

/// <summary>
/// Middleware that loads user permissions from the database and adds them as claims to the current principal.
/// This bridges the gap between external SSO authentication (JWT) and local authorization (DB Permissions).
/// Must be placed AFTER UseAuthentication and BEFORE UseAuthorization.
/// </summary>
public class PermissionLoadingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PermissionLoadingMiddleware> _logger;
    private readonly IMemoryCache _cache;

    public PermissionLoadingMiddleware(
        RequestDelegate next, 
        ILogger<PermissionLoadingMiddleware> logger,
        IMemoryCache cache)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, IUserReadDac userReadDac)
    {
        // 1. Check if user is authenticated
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // 2. Get Local User ID (set by JIT sync in OnTokenValidated)
        var userIdClaim = context.User.FindFirst("x-local-user-id");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            // If we don't have a local ID yet (e.g. JIT failed or first request race condition), 
            // we can't load permissions. Proceed without them (will likely result in 403).
            await _next(context);
            return;
        }

        // 3. Fetch Permissions (with Caching)
        // Cache Key: Permissions:{UserId}
        // Expiration: 1 minute (short enough for updates to propagate, long enough to save DB hits)
        var cacheKey = $"Permissions:{userId}";
        
        if (!_cache.TryGetValue(cacheKey, out List<string>? permissionCodes))
        {
            try 
            {
                var permissions = await userReadDac.GetPermissionsAsync(userId, context.RequestAborted);
                
                // Convert to FastEndpoints permission format: "Module.SubModule.Action"
                // e.g. "TASK_ALLOCATION.MEMBERS_DIST.VIEW"
                permissionCodes = new List<string>();
                
                foreach (var p in permissions)
                {
                    if (p.CanView) permissionCodes.Add($"{p.Module}.{p.SubModule}.VIEW");
                    if (p.CanInsert) permissionCodes.Add($"{p.Module}.{p.SubModule}.INSERT");
                    if (p.CanEdit) permissionCodes.Add($"{p.Module}.{p.SubModule}.EDIT");
                    if (p.CanDelete) permissionCodes.Add($"{p.Module}.{p.SubModule}.DELETE");
                }

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(1))
                    .SetSize(1); // Required because SizeLimit is enabled in Program.cs
                
                _cache.Set(cacheKey, permissionCodes, cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load permissions for user {UserId}", userId);
                // Don't crash the request, just proceed without permissions
                await _next(context);
                return;
            }
        }

        // 4. Add Permissions to ClaimsPrincipal
        if (permissionCodes != null && permissionCodes.Count > 0)
        {
            var claimsIdentity = context.User.Identity as ClaimsIdentity;
            if (claimsIdentity != null)
            {
                foreach (var code in permissionCodes)
                {
                    // FastEndpoints looks for "permissions" claim type by default
                    claimsIdentity.AddClaim(new Claim("permissions", code));
                }
            }
        }

        await _next(context);
    }
}
