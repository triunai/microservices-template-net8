using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Rgt.Space.Core.Domain.Contracts.Identity;
using Rgt.Space.Core.Domain.Entities.Identity;
using Rgt.Space.Core.ReadModels;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Dapper;

namespace Rgt.Space.Tests.Integration.API.Roles;

public class CreateRoleEndpointTests : IClassFixture<SpaceWebApplicationFactory>
{
    private readonly SpaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CreateRoleEndpointTests(SpaceWebApplicationFactory factory)
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
    public async Task CreateRole_ValidRequest_ShouldReturn201()
    {
        var token = await GenerateTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Arrange
        // Code should be uppercase and NO HYPHENS to pass validation ^[A-Z0-9_]+$
        var request = new CreateRoleRequest(
            Name: "Test Role",
            Code: $"TEST_ROLE_{Guid.NewGuid().ToString().Replace("-", "_").ToUpper()}",
            Description: "A test role",
            IsActive: true
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/roles", request);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var role = await response.Content.ReadFromJsonAsync<RoleReadModel>();
        role.Should().NotBeNull();
        role!.Name.Should().Be(request.Name);
        role.Code.Should().Be(request.Code);
    }
}
