namespace Rgt.Space.Core.ReadModels;

public sealed record ClientFeatureDetailReadModel(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    Guid FeatureId,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);
