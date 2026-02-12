namespace Rgt.Space.Core.ReadModels;

public sealed record UserOverrideReadModel(
    Guid Id,
    Guid UserId,
    Guid FeatureId,
    string OverrideState,
    string? Reason,
    DateTime CreatedAt,
    Guid? CreatedBy);
