namespace Rgt.Space.Core.ReadModels;

public sealed record ClientFeatureByClientReadModel(
    Guid Id,
    Guid ClientId,
    Guid FeatureId,
    string FeatureCode,
    string FeatureName,
    bool IsEnabled,
    bool FeatureIsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
