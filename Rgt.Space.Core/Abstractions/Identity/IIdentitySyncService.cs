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
}
