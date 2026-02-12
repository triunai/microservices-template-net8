using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Entities.Identity;
using Rgt.Space.Infrastructure.Persistence.Services.Identity;

namespace Rgt.Space.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class IdentitySyncServiceTests
{
    private readonly IUserReadDac _userReadDac;
    private readonly IUserWriteDac _userWriteDac;
    private readonly ILogger<IdentitySyncService> _logger;
    private readonly IdentitySyncService _sut;

    public IdentitySyncServiceTests()
    {
        _userReadDac = Substitute.For<IUserReadDac>();
        _userWriteDac = Substitute.For<IUserWriteDac>();
        _logger = Substitute.For<ILogger<IdentitySyncService>>();
        _sut = new IdentitySyncService(_userReadDac, _userWriteDac, _logger);
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WhenUserDoesNotExist_ShouldCreateNewUser()
    {
        // Arrange
        var provider = "google";
        var externalId = "google_12345";
        var email = "newuser@example.com";
        var displayName = "New User";

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns((Core.ReadModels.UserReadModel?)null);

        // Act
        await _sut.SyncUserFromSsoAsync(provider, externalId, email, displayName);

        // Assert
        await _userWriteDac.Received(1).CreateAsync(
            Arg.Is<User>(u => 
                u.ExternalId == externalId &&
                u.Email == email &&
                u.DisplayName == displayName &&
                u.SsoProvider == provider),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WhenUserExists_ShouldUpdateExistingUser()
    {
        // Arrange
        var provider = "azuread";
        var externalId = "azure_67890";
        var email = "existinguser@example.com";
        var displayName = "Updated Name";

        var existingUser = User.CreateFromSso(externalId, "old@example.com", "Old Name", provider);

        var readModel = CreateUserReadModel(existingUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(existingUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        await _sut.SyncUserFromSsoAsync(provider, externalId, email, displayName);

        // Assert
        await _userWriteDac.Received(1).UpdateAsync(
            Arg.Is<User>(u => 
                u.Id == existingUser.Id &&
                u.DisplayName == displayName),
            Arg.Any<CancellationToken>());

        await _userWriteDac.Received(1).UpdateLastLoginAsync(
            existingUser.Id,
            provider,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WhenUserExists_ShouldNotCreateDuplicate()
    {
        // Arrange
        var provider = "google";
        var externalId = "google_99999";
        var existingUser = User.CreateFromSso(externalId, "user@example.com", "User", provider);

        var readModel = CreateUserReadModel(existingUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns(readModel);

        // Act
        await _sut.SyncUserFromSsoAsync(provider, externalId, "user@example.com", "User");

        // Assert
        await _userWriteDac.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    private static Core.ReadModels.UserReadModel CreateUserReadModel(User user) =>
        new(
            user.Id,
            user.DisplayName,
            user.Email,
            null,
            user.IsActive,
            user.LocalLoginEnabled,
            user.SsoLoginEnabled,
            user.SsoProvider,
            user.ExternalId,
            user.LastLoginAt,
            user.LastLoginProvider,
            user.CreatedAt,
            null,
            user.UpdatedAt,
            null);

    private static User CreateSoftDeletedUser(string externalId, string email, string provider)
    {
        var user = User.CreateFromSso(externalId, email, "Deleted User", provider);
        user.SoftDelete(Guid.NewGuid());
        return user;
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_ShouldHandleMultipleProvidersCorrectly()
    {
        // Arrange
        var provider1 = "google";
        var provider2 = "azuread";
        var externalId1 = "google_123";
        var externalId2 = "azure_123";
        var email = "user@example.com";

        _userReadDac.GetByExternalIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Core.ReadModels.UserReadModel?)null);

        // Act
        await _sut.SyncUserFromSsoAsync(provider1, externalId1, email, "User 1");
        await _sut.SyncUserFromSsoAsync(provider2, externalId2, email, "User 2");

        // Assert
        await _userWriteDac.Received(2).CreateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());

        await _userWriteDac.Received(1).CreateAsync(
            Arg.Is<User>(u => u.SsoProvider == provider1 && u.ExternalId == externalId1),
            Arg.Any<CancellationToken>());

        await _userWriteDac.Received(1).CreateAsync(
            Arg.Is<User>(u => u.SsoProvider == provider2 && u.ExternalId == externalId2),
            Arg.Any<CancellationToken>());
    }

    // ── Fix 7: Soft-Delete Rejection ─────────────────────────────────────

    [Fact]
    public async Task SyncUserFromSsoAsync_WhenEmailMatchIsSoftDeleted_ShouldRejectAndNotReactivate()
    {
        // Arrange
        var provider = "google";
        var externalId = "google_soft_del_1";
        var email = "deleted@example.com";
        var displayName = "Deleted User";

        var deletedUser = CreateSoftDeletedUser(externalId, email, provider);
        var readModel = CreateUserReadModel(deletedUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns((Core.ReadModels.UserReadModel?)null);

        _userReadDac.GetByEmailAnyAsync(email, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(deletedUser.Id, Arg.Any<CancellationToken>())
            .Returns(deletedUser);

        // Act
        await _sut.SyncUserFromSsoAsync(provider, externalId, email, displayName);

        // Assert
        await _userWriteDac.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userWriteDac.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WhenEmailMatchIsActive_ShouldLinkAndUpdate()
    {
        // Arrange
        var provider = "google";
        var externalId = "google_active_1";
        var email = "active@example.com";
        var displayName = "Active User";

        var activeUser = User.CreateFromSso("old_ext_id", email, "Old Name", "old_provider");

        var readModel = CreateUserReadModel(activeUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns((Core.ReadModels.UserReadModel?)null);

        _userReadDac.GetByEmailAnyAsync(email, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(activeUser.Id, Arg.Any<CancellationToken>())
            .Returns(activeUser);

        // Act
        await _sut.SyncUserFromSsoAsync(provider, externalId, email, displayName);

        // Assert
        await _userWriteDac.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.SsoLoginEnabled && u.SsoProvider == provider && u.ExternalId == externalId),
            Arg.Any<CancellationToken>());

        await _userWriteDac.Received(1).UpdateLastLoginAsync(
            activeUser.Id, provider, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncOrGetUserAsync_WhenEmailMatchIsSoftDeleted_ShouldReturnGuidEmpty()
    {
        // Arrange
        var provider = "azuread";
        var externalId = "azure_soft_del_1";
        var email = "deleted-sync@example.com";
        var displayName = "Deleted Sync User";

        var deletedUser = CreateSoftDeletedUser(externalId, email, provider);
        var readModel = CreateUserReadModel(deletedUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns((Core.ReadModels.UserReadModel?)null);

        _userReadDac.GetByEmailAnyAsync(email, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(deletedUser.Id, Arg.Any<CancellationToken>())
            .Returns(deletedUser);

        // Act
        var result = await _sut.SyncOrGetUserAsync(provider, externalId, email, displayName);

        // Assert
        result.Should().Be(Guid.Empty);
        await _userWriteDac.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncOrGetUserAsync_WhenEmailMatchIsActive_ShouldReturnUserId()
    {
        // Arrange
        var provider = "azuread";
        var externalId = "azure_active_1";
        var email = "active-sync@example.com";
        var displayName = "Active Sync User";

        var activeUser = User.CreateFromSso("old_ext_id", email, "Old Name", "old_provider");

        var readModel = CreateUserReadModel(activeUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns((Core.ReadModels.UserReadModel?)null);

        _userReadDac.GetByEmailAnyAsync(email, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(activeUser.Id, Arg.Any<CancellationToken>())
            .Returns(activeUser);

        // Act
        var result = await _sut.SyncOrGetUserAsync(provider, externalId, email, displayName);

        // Assert
        result.Should().Be(activeUser.Id);
        await _userWriteDac.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.SsoLoginEnabled && u.SsoProvider == provider),
            Arg.Any<CancellationToken>());
    }

    // ── Fix 9: Provider-Agnostic Lookup ──────────────────────────────────

    [Fact]
    public async Task SyncUserFromSsoAsync_ShouldLookupByExternalIdWithoutProvider()
    {
        // Arrange
        var provider = "google";
        var externalId = "ext_provider_agnostic_1";
        var email = "agnostic@example.com";
        var displayName = "Agnostic User";

        var existingUser = User.CreateFromSso(externalId, email, displayName, provider);
        var readModel = CreateUserReadModel(existingUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(existingUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        await _sut.SyncUserFromSsoAsync(provider, externalId, email, displayName);

        // Assert — verify lookup used ONLY externalId (no provider param in signature)
        await _userReadDac.Received(1).GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>());
        await _userReadDac.DidNotReceive().GetByEmailAnyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncOrGetUserAsync_ShouldLookupByExternalIdWithoutProvider()
    {
        // Arrange
        var provider = "azuread";
        var externalId = "ext_provider_agnostic_2";
        var email = "agnostic2@example.com";
        var displayName = "Agnostic User 2";

        var existingUser = User.CreateFromSso(externalId, email, displayName, provider);
        var readModel = CreateUserReadModel(existingUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(existingUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        var result = await _sut.SyncOrGetUserAsync(provider, externalId, email, displayName);

        // Assert — verify lookup used ONLY externalId (no provider param in signature)
        await _userReadDac.Received(1).GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>());
        await _userReadDac.DidNotReceive().GetByEmailAnyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        result.Should().Be(existingUser.Id);
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WhenProviderChanges_ShouldStillFindByExternalId()
    {
        // Arrange
        var originalProvider = "Google";
        var newProvider = "AzureAD";
        var externalId = "ext_provider_change_1";
        var email = "provider-change@example.com";
        var displayName = "Provider Change User";

        var existingUser = User.CreateFromSso(externalId, email, displayName, originalProvider);
        var readModel = CreateUserReadModel(existingUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(existingUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act — call with a DIFFERENT provider than the one stored
        await _sut.SyncUserFromSsoAsync(newProvider, externalId, email, displayName);

        // Assert — user found by external_id regardless of provider mismatch
        await _userWriteDac.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.Id == existingUser.Id),
            Arg.Any<CancellationToken>());

        await _userWriteDac.Received(1).UpdateLastLoginAsync(
            existingUser.Id, newProvider, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncOrGetUserAsync_WhenProviderChanges_ShouldStillFindAndReturnUserId()
    {
        // Arrange
        var originalProvider = "Google";
        var newProvider = "AzureAD";
        var externalId = "ext_provider_change_2";
        var email = "provider-change2@example.com";
        var displayName = "Provider Change User 2";

        var existingUser = User.CreateFromSso(externalId, email, displayName, originalProvider);
        var readModel = CreateUserReadModel(existingUser);

        _userReadDac.GetByExternalIdAsync(externalId, Arg.Any<CancellationToken>())
            .Returns(readModel);

        _userWriteDac.GetByIdAsync(existingUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act — call with a DIFFERENT provider than the one stored
        var result = await _sut.SyncOrGetUserAsync(newProvider, externalId, email, displayName);

        // Assert — user found by external_id regardless of provider mismatch
        result.Should().Be(existingUser.Id);
        await _userWriteDac.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.Id == existingUser.Id),
            Arg.Any<CancellationToken>());
    }
}
