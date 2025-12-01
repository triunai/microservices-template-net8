using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

public sealed class Resource : AuditableEntity
{
    public Guid ModuleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    private Resource(Guid id) : base(id) { }

    public static Resource Create(Guid moduleId, string name, string code, int sortOrder)
    {
        return new Resource(Guid.NewGuid())
        {
            ModuleId = moduleId,
            Name = name,
            Code = code,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
