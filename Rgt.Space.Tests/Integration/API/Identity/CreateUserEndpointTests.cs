using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Rgt.Space.Core.Domain.Contracts.Identity;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace Rgt.Space.Tests.Integration.API.Identity;

public class CreateUserEndpointTests : IClassFixture<SpaceWebApplicationFactory>
{
    private readonly SpaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CreateUserEndpointTests(SpaceWebApplicationFactory factory)
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
            new Claim("email", "admin@example.com"),
            new Claim("name", "Admin User")
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
    public async Task CreateUser_ValidRequest_ShouldCreateUserAndReturn201()
    {
        // Arrange
        var request = new CreateUserRequest(
            DisplayName: "New User",
            Email: $"newuser_{Guid.NewGuid()}@example.com", // Ensure uniqueness
            ContactNumber: "+1234567890",
            LocalLoginEnabled: true,
            Password: "SecurePassword123!",
            RoleIds: null
        );

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateToken());

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var responseBody = await response.Content.ReadFromJsonAsync<CreateUserResponse>();
        responseBody.Should().NotBeNull();
        responseBody!.DisplayName.Should().Be(request.DisplayName);
        responseBody.Email.Should().Be(request.Email);
        responseBody.Id.Should().NotBeEmpty();
    }
}
