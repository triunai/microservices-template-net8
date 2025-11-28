using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Infrastructure.Services.Identity;

/// <summary>
/// Handles Just-In-Time (JIT) provisioning of users from SSO providers.
/// </summary>
public sealed class IdentitySyncService : IIdentitySyncService
{
    private readonly IUserReadDac _userRead;
    private readonly IUserWriteDac _userWrite;
    private readonly ILogger<IdentitySyncService> _logger;

    public IdentitySyncService(
        IUserReadDac userRead,
        IUserWriteDac userWrite,
        ILogger<IdentitySyncService> logger)
    {
        _userRead = userRead;
        _userWrite = userWrite;
        _logger = logger;
    }

    public async Task SyncUserFromSsoAsync(
        string provider,
        string externalId,
        string email,
        string displayName,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Syncing user from SSO. Provider: {Provider}, ExternalId: {ExternalId}, Email: {Email}",
            provider, externalId, email);

        // Try to find existing user by external ID
        var existingUser = await _userRead.GetByExternalIdAsync(provider, externalId, ct);

        if (existingUser is null)
        {
            // User doesn't exist - create new (JIT Provisioning)
            _logger.LogInformation("Creating new user from SSO. Provider: {Provider}, Email: {Email}", provider, email);

            var newUser = User.CreateFromSso(
                externalId: externalId,
                email: email,
                displayName: displayName,
                provider: provider);

            await _userWrite.CreateAsync(newUser, ct);

            _logger.LogInformation("Successfully created user {UserId} from SSO", newUser.Id);
        }
        else
        {
            // User exists - update their info if needed
            _logger.LogDebug("User already exists. UserId: {UserId}. Updating info.", existingUser.Id);

            existingUser.UpdateFromSso(displayName, email);
            existingUser.UpdateLastLogin(provider);

            await _userWrite.UpdateAsync(existingUser, ct);
            await _userWrite.UpdateLastLoginAsync(existingUser.Id, provider, ct);

            _logger.LogDebug("Successfully updated user {UserId} from SSO", existingUser.Id);
        }
    }
}
