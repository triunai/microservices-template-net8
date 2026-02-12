using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

// TODO: SQL table 'user_roles' uses assigned_by_user_id + assigned_at (not standard created_by/created_at),
//       has no updated_at/updated_by/is_deleted/deleted_at/deleted_by columns, but entity inherits AuditableEntity.
//       Future: align column names or create a lightweight entity base for assignment-style tables.
public sealed class UserRole : AuditableEntity
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    private UserRole(Guid id) : base(id) { }

    public static UserRole Create(Guid userId, Guid roleId)
    {
        return new UserRole(Uuid7.NewUuid7())
        {
            UserId = userId,
            RoleId = roleId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
