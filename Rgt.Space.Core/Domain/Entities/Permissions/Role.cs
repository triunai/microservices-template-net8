using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

public sealed class Role : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }

    private Role(Guid id) : base(id) { }

    public static Role Create(string name, string description, bool isSystem = false)
    {
        return new Role(Guid.NewGuid())
        {
            Name = name,
            Description = description,
            IsSystem = isSystem,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
