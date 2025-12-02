namespace Rgt.Space.Core.Abstractions.Identity;

public interface IIdentitySyncService
{
    /// <summary>
    /// Syncs a user from an external SSO provider (JIT Provisioning).
    /// Creates the user if they don't exist, or updates them if they do.
    /// </summary>
    Task SyncUserFromSsoAsync(
        string provider, 
        string externalId, 
        string email, 
        string displayName, 
        CancellationToken ct = default);

    /// <summary>
    /// Syncs user from SSO and returns their Local ID.
    /// Used by OnTokenValidated to attach x-local-user-id claim.
    /// </summary>
    Task<Guid> SyncOrGetUserAsync(string provider, string externalId, string email, string displayName, CancellationToken ct = default);
}
