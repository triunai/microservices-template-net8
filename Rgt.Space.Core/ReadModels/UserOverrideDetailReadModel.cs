namespace Rgt.Space.Core.ReadModels;

public sealed record UserOverrideDetailReadModel(
    Guid Id,
    Guid UserId,
    string UserDisplayName,
    string UserEmail,
    Guid FeatureId,
    string OverrideState,
    string? Reason,
    DateTime CreatedAt,
    Guid? CreatedBy);
