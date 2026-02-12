using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Core.Domain.Entities.Features;

/// <summary>
/// User-level feature override. Uses hard delete (no soft delete).
/// Thin audit: only created_at and created_by.
/// Override state validation is handled by the SetUserOverride handler's FluentValidation.
/// </summary>
public sealed class UserFeatureOverride : Entity
{
    public Guid UserId { get; private set; }
    public Guid FeatureId { get; private set; }
    public string OverrideState { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }

    private UserFeatureOverride(Guid id) : base(id) { }

    public static UserFeatureOverride Create(
        Guid userId,
        Guid featureId,
        string overrideState,
        string? reason = null,
        Guid? createdBy = null)
    {
        return new UserFeatureOverride(Uuid7.NewUuid7())
        {
            UserId = userId,
            FeatureId = featureId,
            OverrideState = overrideState,
            Reason = reason,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static UserFeatureOverride Rehydrate(
        Guid id,
        Guid userId,
        Guid featureId,
        string overrideState,
        string? reason,
        DateTime createdAt,
        Guid? createdBy)
    {
        return new UserFeatureOverride(id)
        {
            UserId = userId,
            FeatureId = featureId,
            OverrideState = overrideState,
            Reason = reason,
            CreatedAt = createdAt,
            CreatedBy = createdBy
        };
    }
}
