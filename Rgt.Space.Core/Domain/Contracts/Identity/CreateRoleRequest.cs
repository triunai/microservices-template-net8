namespace Rgt.Space.Core.Domain.Contracts.Identity;

/// <summary>
/// Request DTO for creating a new role.
/// </summary>
public sealed record CreateRoleRequest(
    string Name,
    string Code,
    string? Description,
    bool IsActive = true
);
