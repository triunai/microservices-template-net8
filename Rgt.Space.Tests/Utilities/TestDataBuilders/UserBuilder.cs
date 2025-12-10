using Bogus;
using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Tests.Utilities.TestDataBuilders;

public static class UserBuilder
{
    private static readonly Faker Faker = new();

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
