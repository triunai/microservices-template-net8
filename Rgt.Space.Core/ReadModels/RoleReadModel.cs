namespace Rgt.Space.Core.ReadModels;

/// <summary>
/// Read model for Role entity.
/// </summary>
public sealed record RoleReadModel(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsSystem,
    bool IsActive,
    int UserCount,
    DateTime CreatedAt,
    Guid? CreatedBy,
    DateTime UpdatedAt,
    Guid? UpdatedBy
);
