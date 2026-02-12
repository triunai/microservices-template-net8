namespace Rgt.Space.Core.ReadModels;

public sealed record ClientFeatureReadModel(
    Guid Id,
    Guid ClientId,
    Guid FeatureId,
    bool IsEnabled,
    DateTime CreatedAt,
    Guid? CreatedBy,
    DateTime UpdatedAt,
    Guid? UpdatedBy);
