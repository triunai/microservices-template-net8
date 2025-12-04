namespace Rgt.Space.Core.Domain.Contracts.Identity;

/// <summary>
/// Request DTO for updating a role.
/// Note: Code cannot be changed after creation.
/// </summary>
public sealed record UpdateRoleRequest(
    string Name,
    string? Description,
    bool IsActive
);
