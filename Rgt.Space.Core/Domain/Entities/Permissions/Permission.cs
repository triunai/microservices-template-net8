using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

// TODO: SQL table 'permissions' has no is_deleted/deleted_at/deleted_by columns but entity inherits AuditableEntity.
//       Future: create TrackedEntity base (audit without soft-delete) or add soft-delete columns to SQL.
public sealed class Permission : AuditableEntity
{
    public Guid ResourceId { get; private set; }
    public Guid ActionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private Permission(Guid id) : base(id) { }

    public static Permission Create(Guid resourceId, Guid actionId, string code, string description)
    {
        return new Permission(Uuid7.NewUuid7())
        {
            ResourceId = resourceId,
            ActionId = actionId,
            Code = code,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
