using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Features;

public sealed class ClientFeature : AuditableEntity
{
    public Guid ClientId { get; private set; }
    public Guid FeatureId { get; private set; }
    public bool IsEnabled { get; private set; }

    private ClientFeature(Guid id) : base(id) { }

    public static ClientFeature Create(
        Guid clientId,
        Guid featureId,
        bool isEnabled = true,
        Guid? createdBy = null)
    {
        return new ClientFeature(Uuid7.NewUuid7())
        {
            ClientId = clientId,
            FeatureId = featureId,
            IsEnabled = isEnabled,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static ClientFeature Rehydrate(
        Guid id,
        Guid clientId,
        Guid featureId,
        bool isEnabled,
        DateTime createdAt,
        Guid? createdBy,
        DateTime updatedAt,
        Guid? updatedBy,
        bool isDeleted,
        DateTime? deletedAt,
        Guid? deletedBy)
    {
        return new ClientFeature(id)
        {
            ClientId = clientId,
            FeatureId = featureId,
            IsEnabled = isEnabled,
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
