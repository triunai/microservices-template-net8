using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Entities.Identity;
using Rgt.Space.Infrastructure.Services.Identity;

namespace Rgt.Space.Tests.Unit.Services;

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

        _userReadDac.GetByExternalIdAsync(provider, externalId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

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

        _userReadDac.GetByExternalIdAsync(provider, externalId, Arg.Any<CancellationToken>())
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

        _userReadDac.GetByExternalIdAsync(provider, externalId, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        await _sut.SyncUserFromSsoAsync(provider, externalId, "user@example.com", "User");

        // Assert
        await _userWriteDac.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
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

        _userReadDac.GetByExternalIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

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
}
