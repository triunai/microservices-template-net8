using Rgt.Space.Core.Abstractions.Identity;

namespace Rgt.Space.Infrastructure.Identity;

/// <summary>
/// Mock implementation for local development without SSO.
/// Returns the System Admin user context.
/// </summary>
public sealed class DevCurrentUser : ICurrentUser
{
    // Hardcoded System Admin ID (from seed data)
    private static readonly Guid SystemAdminId = Guid.Parse("019ac92a-de20-7793-b8df-b88a87ea4e34");

    public Guid Id => SystemAdminId;

    public string? ExternalId => "dev-admin-sub";

    public string? Email => "admin@rgtspace.com";

    public string? TenantKey => "RGT_SPACE_PORTAL";

    public bool IsAuthenticated => true;
}
