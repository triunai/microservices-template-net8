using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.Identity;

public sealed class Tenant : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string ConnectionString { get; private set; } = string.Empty;
    public string Status { get; private set; } = StatusConstants.Active;

    private Tenant(Guid id) : base(id) { }

    public static Tenant Create(string name, string code, string connectionString)
    {
        return new Tenant(Guid.NewGuid())
        {
            Name = name,
            Code = code,
            ConnectionString = connectionString,
            Status = StatusConstants.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
