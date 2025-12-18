using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Rgt.Space.Core.Domain.Entities.Identity;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Dapper;

namespace Rgt.Space.Tests.Integration.API.Identity;

public class DeleteUserEndpointTests : IClassFixture<SpaceWebApplicationFactory>
{
    private readonly SpaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeleteUserEndpointTests(SpaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<string> GenerateTokenAsync()
    {
        var adminEmail = "admin@example.com";
        var adminName = "Admin User";

        await using var conn = new NpgsqlConnection(_factory.DbFixture.ConnectionString);
        await conn.OpenAsync();

        // Try to find existing admin
        var adminId = await conn.QuerySingleOrDefaultAsync<Guid?>("SELECT id FROM users WHERE email = @Email", new { Email = adminEmail });

        if (adminId == null)
        {
            var newAdmin = User.CreateManual(adminName, adminEmail, null, true, null, null, Guid.Empty);
            adminId = newAdmin.Id;

            var sql = @"
                INSERT INTO users (id, display_name, email, is_active, local_login_enabled, created_at, updated_at, is_deleted)
                VALUES (@Id, @DisplayName, @Email, @IsActive, @LocalLoginEnabled, @CreatedAt, @UpdatedAt, @IsDeleted)
                ON CONFLICT (email) DO NOTHING";

            await conn.ExecuteAsync(sql, new
            {
                Id = adminId,
                DisplayName = newAdmin.DisplayName,
                Email = newAdmin.Email,
                IsActive = newAdmin.IsActive,
                LocalLoginEnabled = newAdmin.LocalLoginEnabled,
                CreatedAt = newAdmin.CreatedAt,
                UpdatedAt = newAdmin.UpdatedAt,
                IsDeleted = newAdmin.IsDeleted
            });

            // Re-fetch ID just in case race condition in parallel tests
            var existing = await conn.QuerySingleOrDefaultAsync<Guid?>("SELECT id FROM users WHERE email = @Email", new { Email = adminEmail });
            if (existing != null) adminId = existing;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSigningKey_MustBe32CharsLong12345"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", adminId.ToString()),
            new Claim("email", adminEmail),
            new Claim("name", adminName)
        };

        var token = new JwtSecurityToken(
            issuer: "rgt-space-portal",
            audience: "rgt-space-portal-api",
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task DeleteUser_WhenUserExists_ShouldReturn200()
    {
        // Arrange
        var token = await GenerateTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var user = User.CreateFromSso("ext_del_1", "del_1@test.com", "Delete Me", "google");

        await using var conn = new NpgsqlConnection(_factory.DbFixture.ConnectionString);
        await conn.OpenAsync();
        var sql = @"
            INSERT INTO users (id, display_name, email, is_active, sso_provider, external_id, created_at, updated_at, is_deleted)
            VALUES (@Id, @DisplayName, @Email, @IsActive, @SsoProvider, @ExternalId, @CreatedAt, @UpdatedAt, @IsDeleted)";

        await conn.ExecuteAsync(sql, new
        {
            user.Id,
            user.DisplayName,
            user.Email,
            user.IsActive,
            user.SsoProvider,
            user.ExternalId,
            user.CreatedAt,
            user.UpdatedAt,
            user.IsDeleted
        });

        // Act
        var response = await _client.DeleteAsync($"/api/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Soft Delete
        var deletedUser = await conn.QuerySingleOrDefaultAsync<dynamic>("SELECT is_deleted, deleted_at, deleted_by FROM users WHERE id = @Id", new { user.Id });
        Assert.True(deletedUser.is_deleted);
        Assert.NotNull(deletedUser.deleted_at);
        Assert.NotNull(deletedUser.deleted_by);
    }

    [Fact]
    public async Task DeleteUser_WhenUserDoesNotExist_ShouldReturn404()
    {
        var token = await GenerateTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
