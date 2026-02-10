namespace Rgt.Space.Core.ReadModels;

public sealed record UserOverrideByUserReadModel(
    Guid Id,
    Guid UserId,
    Guid FeatureId,
    string FeatureCode,
    string FeatureName,
    string OverrideState,
    string? Reason,
    DateTime CreatedAt,
    Guid? CreatedBy);
