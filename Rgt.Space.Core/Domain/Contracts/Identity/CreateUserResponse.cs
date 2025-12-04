namespace Rgt.Space.Core.Domain.Contracts.Identity;

/// <summary>
/// Response DTO for Create User endpoint.
/// </summary>
public sealed record CreateUserResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string? ContactNumber,
    bool IsActive,
    bool LocalLoginEnabled,
    bool SsoLoginEnabled,
    List<RoleInfo>? Roles,
    DateTime CreatedAt
);

/// <summary>
/// Minimal role info for embedding in user responses.
/// </summary>
public sealed record RoleInfo(
    Guid RoleId,
    string RoleName
);
