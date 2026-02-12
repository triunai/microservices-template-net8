namespace Rgt.Space.Core.Abstractions.Features;

public interface IFeatureWriteDac
{
    /// <summary>
    /// Create a new feature. Returns the new feature ID.
    /// </summary>
    Task<Guid> CreateFeatureAsync(Guid id, string code, string name, string? description,
        bool isActive, bool requiresClient, Guid createdBy, CancellationToken ct);

    /// <summary>
    /// Update feature properties explicitly. Sets updated_at and updated_by.
    /// CRITICAL #8: Full PUT with explicit values, not toggle.
    /// Returns true if a row was updated, false if feature was concurrently deleted.
    /// </summary>
    /// <remarks>
    /// Returns bool (not Task) because the SQL WHERE clause filters is_deleted = FALSE.
    /// A concurrent soft-delete between the handler's existence check and this write
    /// would cause 0 rows affected — the caller must detect this and fail gracefully
    /// instead of returning a silent 204 for an operation that did nothing.
    /// </remarks>
    Task<bool> UpdateFeatureAsync(Guid featureId, string name, string? description,
        bool isActive, bool requiresClient, Guid updatedBy, CancellationToken ct);

    /// <summary>
    /// Soft-delete a feature. Sets is_deleted, deleted_at, deleted_by.
    /// Returns true if a row was deleted, false if feature was already deleted.
    /// </summary>
    /// <remarks>
    /// Same TOCTOU rationale as UpdateFeatureAsync — concurrent deletes cause 0 rows affected.
    /// </remarks>
    Task<bool> SoftDeleteFeatureAsync(Guid featureId, Guid deletedBy, CancellationToken ct);

    /// <summary>
    /// Upsert client feature subscription.
    /// If no row exists: INSERT with specified is_enabled.
    /// If row exists: SET is_enabled to specified value (idempotent).
    /// </summary>
    Task UpsertClientFeatureAsync(Guid clientId, Guid featureId, bool isEnabled, Guid updatedBy, CancellationToken ct);

    /// <summary>
    /// Soft-delete a client feature subscription.
    /// </summary>
    Task SoftDeleteClientFeatureAsync(Guid clientId, Guid featureId, Guid deletedBy, CancellationToken ct);

    /// <summary>
    /// Insert or update a user feature override.
    /// </summary>
    Task SetUserOverrideAsync(Guid userId, Guid featureId, string overrideState,
        string? reason, Guid createdBy, CancellationToken ct);

    /// <summary>
    /// Hard-delete a user feature override.
    /// </summary>
    Task ClearUserOverrideAsync(Guid userId, Guid featureId, CancellationToken ct);
}
