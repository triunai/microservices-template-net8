using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

// TODO: SQL table 'role_permissions' is a 2-column junction (role_id, permission_id composite PK)
//       with no id, audit, or soft-delete columns. Entity inherits AuditableEntity with full audit trail.
//       Future: align by either adding audit columns to SQL or creating a lightweight junction entity base.
public sealed class RolePermission : AuditableEntity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    private RolePermission(Guid id) : base(id) { }

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        return new RolePermission(Uuid7.NewUuid7())
        {
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
