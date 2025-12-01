using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.PortalRouting;

public sealed class Project : AuditableEntity
{
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? ExternalUrl { get; private set; }
    public string Status { get; private set; } = "Active";

    private Project(Guid id) : base(id) { }

    public static Project Create(Guid clientId, string name, string code, string? externalUrl = null, string status = "Active")
    {
        return new Project(Guid.NewGuid())
        {
            ClientId = clientId,
            Name = name,
            Code = code,
            ExternalUrl = externalUrl,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
