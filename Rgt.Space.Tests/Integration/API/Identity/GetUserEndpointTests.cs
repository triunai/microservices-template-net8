using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Rgt.Space.Core.Domain.Contracts.Identity;
using Rgt.Space.Core.Domain.Entities.Identity;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Dapper;

namespace Rgt.Space.Tests.Integration.API.Identity;

public class GetUserEndpointTests : IClassFixture<SpaceWebApplicationFactory>
{
    private readonly SpaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GetUserEndpointTests(SpaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private string GenerateToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSigningKey_MustBe32CharsLong12345"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("email", "test@example.com")
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
    public async Task GetUser_WhenUserExists_ShouldReturn200AndUser()
    {
        // Arrange
        var user = User.CreateFromSso("api_test_ext", "api_test@example.com", "API Test User", "google");

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

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateToken());

        // Act
        var response = await _client.GetAsync($"/api/v1/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var returnedUser = await response.Content.ReadFromJsonAsync<UserResponse>();
        returnedUser.Should().NotBeNull();
        returnedUser!.Id.Should().Be(user.Id);
        returnedUser.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetUser_WhenUserDoesNotExist_ShouldReturn404()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateToken());

        // Act
        var response = await _client.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
