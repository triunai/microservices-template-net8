using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Infrastructure.Persistence.Services.Identity;

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
        var existingUserReadModel = await _userRead.GetByExternalIdAsync(provider, externalId, ct);

        if (existingUserReadModel is null)
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
            _logger.LogDebug("User already exists. UserId: {UserId}. Fetching full entity for update.", existingUserReadModel.Id);

            var userEntity = await _userWrite.GetByIdAsync(existingUserReadModel.Id, ct);

            if (userEntity is null)
            {
                _logger.LogError("User {UserId} found in ReadModel but not in WriteModel (Entity). Data inconsistency detected.", existingUserReadModel.Id);
                return;
            }

            userEntity.UpdateFromSso(displayName, email);
            userEntity.UpdateLastLogin(provider);

            await _userWrite.UpdateAsync(userEntity, ct);
            await _userWrite.UpdateLastLoginAsync(userEntity.Id, provider, ct);

            _logger.LogDebug("Successfully updated user {UserId} from SSO", userEntity.Id);
        }
    }
}
