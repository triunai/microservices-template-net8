using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

public sealed class RolePermission : AuditableEntity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    private RolePermission(Guid id) : base(id) { }

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        return new RolePermission(Guid.NewGuid())
        {
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
