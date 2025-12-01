using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

public sealed class UserPermissionOverride : AuditableEntity
{
    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public bool IsAllowed { get; private set; }

    private UserPermissionOverride(Guid id) : base(id) { }

    public static UserPermissionOverride Create(Guid userId, Guid permissionId, bool isAllowed)
    {
        return new UserPermissionOverride(Guid.NewGuid())
        {
            UserId = userId,
            PermissionId = permissionId,
            IsAllowed = isAllowed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
