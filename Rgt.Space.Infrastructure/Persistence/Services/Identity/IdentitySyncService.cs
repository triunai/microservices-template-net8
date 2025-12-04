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

        // 1. Try to find by External ID (Already Linked)
        var existingUserReadModel = await _userRead.GetByExternalIdAsync(provider, externalId, ct);

        if (existingUserReadModel is null)
        {
            // 2. Try to find by Email (Pre-seeded, Legacy, or Deleted)
            // We use GetByEmailAnyAsync to find even deleted users to prevent unique constraint violations
            existingUserReadModel = await _userRead.GetByEmailAnyAsync(email, ct);

            if (existingUserReadModel is not null)
            {
                _logger.LogInformation("User found by email (Active or Deleted). Linking/Reactivating now. UserId: {UserId}", existingUserReadModel.Id);
                
                var userEntity = await _userWrite.GetByIdAsync(existingUserReadModel.Id, ct);
                if (userEntity != null)
                {
                    // If user was deleted, reactivate them
                    if (userEntity.IsDeleted)
                    {
                        _logger.LogInformation("Reactivating deleted user {UserId}", userEntity.Id);
                        userEntity.Reactivate();
                    }

                    // Link the account
                    userEntity.LinkSso(provider, externalId, email);
                    userEntity.UpdateFromSso(displayName, email);
                    userEntity.UpdateLastLogin(provider);
                    
                    await _userWrite.UpdateAsync(userEntity, ct);
                    await _userWrite.UpdateLastLoginAsync(userEntity.Id, provider, ct);
                    return;
                }
            }

            // 3. User doesn't exist at all - create new (JIT Provisioning)
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
            // 4. User exists and is linked - update info
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


    public async Task<Guid> SyncOrGetUserAsync(
        string provider,
        string externalId,
        string email,
        string displayName,
        CancellationToken ct = default)
    {
        // 1. Try to find by External ID
        var existingUserReadModel = await _userRead.GetByExternalIdAsync(provider, externalId, ct);

        if (existingUserReadModel is null)
        {
            // 2. Try to find by Email (Any status)
            existingUserReadModel = await _userRead.GetByEmailAnyAsync(email, ct);

            if (existingUserReadModel != null)
            {
                // Link existing user
                var userEntity = await _userWrite.GetByIdAsync(existingUserReadModel.Id, ct);
                if (userEntity != null)
                {
                    if (userEntity.IsDeleted)
                    {
                        userEntity.Reactivate();
                    }

                    userEntity.LinkSso(provider, externalId, email);
                    userEntity.UpdateFromSso(displayName, email);
                    userEntity.UpdateLastLogin(provider);
                    await _userWrite.UpdateAsync(userEntity, ct);
                    await _userWrite.UpdateLastLoginAsync(userEntity.Id, provider, ct);
                    return userEntity.Id;
                }
            }

            // 3. Create New
            var newUser = User.CreateFromSso(externalId, email, displayName, provider);
            await _userWrite.CreateAsync(newUser, ct);
            return newUser.Id;
        }
        else
        {
            // 4. Update Existing
            var userEntity = await _userWrite.GetByIdAsync(existingUserReadModel.Id, ct);
            if (userEntity != null)
            {
                userEntity.UpdateFromSso(displayName, email);
                userEntity.UpdateLastLogin(provider);
                await _userWrite.UpdateAsync(userEntity, ct);
                await _userWrite.UpdateLastLoginAsync(userEntity.Id, provider, ct);
            }
            
            return existingUserReadModel.Id;
        }
    }
}
