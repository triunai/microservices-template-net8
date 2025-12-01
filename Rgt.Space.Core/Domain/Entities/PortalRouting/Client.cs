using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.PortalRouting;

public sealed class Client : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Active";

    private Client(Guid id) : base(id) { }

    public static Client Create(string name, string code, string status = "Active")
    {
        return new Client(Guid.NewGuid())
        {
            Name = name,
            Code = code,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
