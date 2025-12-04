namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

public record CreateProjectRequest(
    Guid ClientId,
    string Name,
    string Code,
    string Status,
    string? ExternalUrl);
