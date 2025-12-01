namespace Rgt.Space.Core.ReadModels;

public sealed record ClientProjectMappingReadModel(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    string RoutingUrl,
    string Environment,
    string Status,
    DateTime CreatedAt
);
