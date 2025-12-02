using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Rgt.Space.Core.Abstractions.Identity;

namespace Rgt.Space.Infrastructure.Identity;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid Id
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("x-local-user-id");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
        }
    }

    public string? ExternalId => _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;

    public string? TenantKey => _httpContextAccessor.HttpContext?.User.FindFirst("tid")?.Value;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
