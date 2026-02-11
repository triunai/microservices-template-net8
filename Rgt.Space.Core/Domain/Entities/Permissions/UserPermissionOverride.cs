using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

public sealed class UserPermissionOverride : AuditableEntity
{
    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public bool IsAllowed { get; private set; }
    public string? Reason { get; private set; }

    private UserPermissionOverride(Guid id) : base(id) { }

    public static UserPermissionOverride Create(Guid userId, Guid permissionId, bool isAllowed, string? reason = null)
    {
        return new UserPermissionOverride(Uuid7.NewUuid7())
        {
            UserId = userId,
            PermissionId = permissionId,
            IsAllowed = isAllowed,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
