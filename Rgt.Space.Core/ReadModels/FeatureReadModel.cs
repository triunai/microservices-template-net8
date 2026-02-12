namespace Rgt.Space.Core.ReadModels;

public sealed record FeatureReadModel(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    bool RequiresClient,
    DateTime CreatedAt,
    Guid? CreatedBy,
    DateTime UpdatedAt,
    Guid? UpdatedBy);
