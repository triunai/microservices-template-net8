namespace Rgt.Space.Core.ReadModels;

public sealed record ClientReadModel(
    Guid Id,
    string Name,
    string Code,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
