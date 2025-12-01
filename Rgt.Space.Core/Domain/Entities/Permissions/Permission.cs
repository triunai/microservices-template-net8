using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

public sealed class Permission : AuditableEntity
{
    public Guid ResourceId { get; private set; }
    public Guid ActionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private Permission(Guid id) : base(id) { }

    public static Permission Create(Guid resourceId, Guid actionId, string code, string description)
    {
        return new Permission(Guid.NewGuid())
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
