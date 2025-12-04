namespace Rgt.Space.Core.Domain.Contracts.Identity;

/// <summary>
/// Request DTO for assigning a role to a user.
/// </summary>
public sealed record AssignRoleRequest(
    Guid RoleId
);
