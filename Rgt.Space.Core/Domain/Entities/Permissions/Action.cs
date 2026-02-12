using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

// TODO: SQL table 'actions' has no is_deleted/deleted_at/deleted_by columns but entity inherits AuditableEntity.
//       Future: create TrackedEntity base (audit without soft-delete) or add soft-delete columns to SQL.
public sealed class Action : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;

    private Action(Guid id) : base(id) { }

    public static Action Create(string name, string code)
    {
        return new Action(Uuid7.NewUuid7())
        {
            Name = name,
            Code = code,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
