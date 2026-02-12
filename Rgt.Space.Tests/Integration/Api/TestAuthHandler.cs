using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.Tests.Integration.Api;

/// <summary>
/// Test authentication handler that auto-authenticates as the DevAdmin user
/// with all permissions. Used by integration tests to bypass JWT auth.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    private static readonly Guid DevAdminId = Guid.Parse("019ac92a-de20-7793-b8df-b88a87ea4e34");

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new("x-local-user-id", DevAdminId.ToString()),
            new("sub", DevAdminId.ToString()),
            new("email", "admin@rgtspace.com"),
            new(ClaimTypes.Name, "System Admin"),
            new("tid", "TEST_TENANT"),
            // Feature flag permissions
            new("permissions", FeatureFlagConstants.Permissions.ListView),
            new("permissions", FeatureFlagConstants.Permissions.GlobalEdit),
            new("permissions", FeatureFlagConstants.Permissions.ClientEdit),
            new("permissions", FeatureFlagConstants.Permissions.OverrideInsert),
            new("permissions", FeatureFlagConstants.Permissions.OverrideDelete),
            new("permissions", FeatureFlagConstants.Permissions.EvaluateView),
            // Portal routing permissions
            new("permissions", PermissionConstants.PortalRouting.ClientView),
            new("permissions", PermissionConstants.PortalRouting.ClientInsert),
            new("permissions", PermissionConstants.PortalRouting.ClientEdit),
            new("permissions", PermissionConstants.PortalRouting.ClientDelete),
            new("permissions", PermissionConstants.PortalRouting.RoutingView),
            new("permissions", PermissionConstants.PortalRouting.RoutingInsert),
            new("permissions", PermissionConstants.PortalRouting.RoutingEdit),
            new("permissions", PermissionConstants.PortalRouting.RoutingDelete),
            // Task allocation permissions
            new("permissions", PermissionConstants.TaskAllocation.View),
            new("permissions", PermissionConstants.TaskAllocation.Insert),
            new("permissions", PermissionConstants.TaskAllocation.Edit),
            new("permissions", PermissionConstants.TaskAllocation.Delete),
            // User management permissions
            new("permissions", PermissionConstants.UserManagement.AccountView),
            new("permissions", PermissionConstants.UserManagement.AccountInsert),
            new("permissions", PermissionConstants.UserManagement.AccountEdit),
            new("permissions", PermissionConstants.UserManagement.AccountDelete),
            new("permissions", PermissionConstants.UserManagement.AccessView),
            new("permissions", PermissionConstants.UserManagement.AccessInsert),
            new("permissions", PermissionConstants.UserManagement.AccessEdit),
            new("permissions", PermissionConstants.UserManagement.AccessDelete),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
