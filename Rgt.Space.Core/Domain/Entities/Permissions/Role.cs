using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

// TODO: SQL table 'roles' has no is_deleted/deleted_at/deleted_by columns but entity inherits AuditableEntity.
//       Future: create TrackedEntity base (audit without soft-delete) or add soft-delete columns to SQL.
public sealed class Role : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Role(Guid id) : base(id) { }

    public static Role Create(string name, string code, string? description = null, bool isSystem = false, bool isActive = true)
    {
        return new Role(Uuid7.NewUuid7())
        {
            Name = name,
            Code = code,
            Description = description ?? string.Empty,
            IsSystem = isSystem,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
