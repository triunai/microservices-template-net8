using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.PortalRouting;

/// <summary>
/// Domain entity for client-project mapping (Portal Routing).
/// NOTE: This entity is currently unused. The system uses ClientProjectMappingReadModel instead.
/// Kept for potential future domain-driven design refactoring.
/// </summary>
public sealed class ClientProjectMapping : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public string RoutingUrl { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public string Status { get; private set; } = StatusConstants.Active;

    private ClientProjectMapping(Guid id) : base(id) { }

    public static ClientProjectMapping Create(Guid projectId, string routingUrl, string environment, string status = StatusConstants.Active)
    {
        return new ClientProjectMapping(Guid.NewGuid())
        {
            ProjectId = projectId,
            RoutingUrl = routingUrl,
            Environment = environment,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateStatus(string status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}
