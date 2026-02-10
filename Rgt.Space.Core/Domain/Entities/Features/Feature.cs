using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Features;

public sealed class Feature : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public bool RequiresClient { get; private set; } = true;

    private Feature(Guid id) : base(id) { }

    public static Feature Create(
        string code,
        string name,
        string? description,
        bool isActive = false,
        bool requiresClient = true,
        Guid? createdBy = null)
    {
        return new Feature(Uuid7.NewUuid7())
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name,
            Description = description,
            IsActive = isActive,
            RequiresClient = requiresClient,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Feature Rehydrate(
        Guid id,
        string code,
        string name,
        string? description,
        bool isActive,
        bool requiresClient,
        DateTime createdAt,
        Guid? createdBy,
        DateTime updatedAt,
        Guid? updatedBy,
        bool isDeleted,
        DateTime? deletedAt,
        Guid? deletedBy)
    {
        return new Feature(id)
        {
            Code = code,
            Name = name,
            Description = description,
            IsActive = isActive,
            RequiresClient = requiresClient,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy
        };
    }

}
