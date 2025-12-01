namespace Rgt.Space.Core.ReadModels;

public sealed record ProjectReadModel(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string Name,
    string Code,
    string? ExternalUrl,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
