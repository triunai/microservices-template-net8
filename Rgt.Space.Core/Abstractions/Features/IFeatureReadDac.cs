using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Core.Abstractions.Features;

public interface IFeatureReadDac
{
    /// <summary>
    /// Get feature by code. Query MUST filter is_deleted = FALSE.
    /// </summary>
    Task<FeatureReadModel?> GetByCodeAsync(string code, CancellationToken ct);

    /// <summary>
    /// Get client subscription. Query MUST filter is_deleted = FALSE on both client_features and clients.
    /// </summary>
    Task<ClientFeatureReadModel?> GetClientFeatureAsync(Guid clientId, Guid featureId, CancellationToken ct);

    /// <summary>
    /// Get user override. No is_deleted filter needed (table uses hard delete).
    /// </summary>
    Task<UserOverrideReadModel?> GetUserOverrideAsync(Guid userId, Guid featureId, CancellationToken ct);

    /// <summary>
    /// Returns all non-deleted features (for admin UI listing).
    /// </summary>
    Task<IReadOnlyList<FeatureReadModel>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Get feature by ID. Query MUST filter is_deleted = FALSE.
    /// </summary>
    Task<FeatureReadModel?> GetByIdAsync(Guid featureId, CancellationToken ct);

    /// <summary>
    /// List client subscriptions for a feature, joined with client name/code.
    /// Filters: client_features.is_deleted = FALSE, clients.is_deleted = FALSE.
    /// </summary>
    Task<IReadOnlyList<ClientFeatureDetailReadModel>> GetClientSubscriptionsByFeatureAsync(Guid featureId, CancellationToken ct);

    /// <summary>
    /// List feature subscriptions for a client, joined with feature code/name/isActive.
    /// Filters: client_features.is_deleted = FALSE, features.is_deleted = FALSE.
    /// </summary>
    Task<IReadOnlyList<ClientFeatureByClientReadModel>> GetFeaturesByClientAsync(Guid clientId, CancellationToken ct);

    /// <summary>
    /// List user overrides for a feature, joined with user display name/email.
    /// Filters: users.is_active = TRUE (no soft delete on user_feature_overrides).
    /// </summary>
    Task<IReadOnlyList<UserOverrideDetailReadModel>> GetUserOverridesByFeatureAsync(Guid featureId, CancellationToken ct);

    /// <summary>
    /// List feature overrides for a user, joined with feature code/name.
    /// Filters: features.is_deleted = FALSE.
    /// </summary>
    Task<IReadOnlyList<UserOverrideByUserReadModel>> GetOverridesByUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// All client subscriptions for a client (no JOIN — bulk evaluation).
    /// Filters: is_deleted = FALSE.
    /// </summary>
    Task<IReadOnlyList<ClientFeatureReadModel>> GetClientFeaturesByClientAsync(Guid clientId, CancellationToken ct);

    /// <summary>
    /// All user overrides for a user (no JOIN — bulk evaluation).
    /// No is_deleted filter (table uses hard delete).
    /// </summary>
    Task<IReadOnlyList<UserOverrideReadModel>> GetUserOverridesByUserAsync(Guid userId, CancellationToken ct);
}
