namespace Rgt.Space.Core.ReadModels;

/// <summary>
/// Read model for user's role assignment.
/// </summary>
public sealed record UserRoleReadModel(
    Guid Id,            // user_roles.id
    Guid RoleId,
    string RoleName,
    string RoleCode,
    DateTime AssignedAt,
    string? AssignedByName  // Display name of assigner
);
