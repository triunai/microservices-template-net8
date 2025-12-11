using Bogus;
using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Tests.Utilities.TestDataBuilders;

/// <summary>
/// Test data builder for <see cref="User"/> entities using Bogus.
/// </summary>
public static class UserBuilder
{
    private static readonly Faker Faker = new();

    /// <summary>
    /// Creates a valid <see cref="User"/> instance with random or specified data.
    /// </summary>
    /// <param name="externalId">Optional external ID.</param>
    /// <param name="email">Optional email.</param>
    /// <param name="displayName">Optional display name.</param>
    /// <param name="provider">Optional SSO provider.</param>
    /// <returns>A new <see cref="User"/> instance.</returns>
    public static User Create(
        string? externalId = null,
        string? email = null,
        string? displayName = null,
        string? provider = null)
    {
        return User.CreateFromSso(
            externalId ?? $"ext_{Faker.Random.AlphaNumeric(10)}",
            email ?? Faker.Internet.Email(),
            displayName ?? Faker.Name.FullName(),
            provider ?? Faker.PickRandom(new[] { "google", "azuread", "okta" }));
    }

    public static User WithLastLogin(this User user, string? provider = null)
    {
        user.UpdateLastLogin(provider ?? user.SsoProvider ?? "google");
        return user;
    }
}
