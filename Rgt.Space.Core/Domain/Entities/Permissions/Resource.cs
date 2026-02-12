using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

public sealed class Resource : AuditableEntity
{
    public Guid ModuleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;

    private Resource(Guid id) : base(id) { }

    public static Resource Create(Guid moduleId, string name, string code)
    {
        return new Resource(Uuid7.NewUuid7())
        {
            ModuleId = moduleId,
            Name = name,
            Code = code,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
