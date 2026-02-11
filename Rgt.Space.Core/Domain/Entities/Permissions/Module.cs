using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Permissions;

public sealed class Module : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public int? SortOrder { get; private set; }

    private Module(Guid id) : base(id) { }

    public static Module Create(string name, string code, bool isActive = true, int? sortOrder = null)
    {
        return new Module(Uuid7.NewUuid7())
        {
            Name = name,
            Code = code,
            IsActive = isActive,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
