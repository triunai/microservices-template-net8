namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

public record UpdateProjectRequest(
    string Name,
    string Code,
    string Status,
    string? ExternalUrl);
