using System.Net;
using System.Net.Http.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Infrastructure.Identity;
using Rgt.Space.Tests.Integration.Fixtures;

namespace Rgt.Space.Tests.Integration.Api;

[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class FeatureEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestDbFixture _dbFixture;

    private static readonly Guid DevAdminId = Guid.Parse("019ac92a-de20-7793-b8df-b88a87ea4e34");

    public FeatureEndpointTests(CustomWebApplicationFactory factory, TestDbFixture dbFixture)
    {
        _dbFixture = dbFixture;
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISystemConnectionFactory>();
                services.AddSingleton<ISystemConnectionFactory>(
                    new TestSystemConnectionFactory(dbFixture.ConnectionString));

                // Also override ITenantConnectionFactory (some DACs may use it)
                services.RemoveAll<ITenantConnectionFactory>();
                services.AddSingleton<ITenantConnectionFactory>(
                    new TestSystemConnectionFactory(dbFixture.ConnectionString));

                // Override ICurrentUser with DevCurrentUser (returns hardcoded DevAdmin ID)
                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser, DevCurrentUser>();

                // Test auth: auto-authenticate as DevAdmin with all feature flag permissions
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
                services.PostConfigure<AuthenticationOptions>(o =>
                {
                    o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                });
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:PortalDb", dbFixture.ConnectionString },
                    { "ConnectionStrings:Redis", "localhost:6379" },
                    { "Auth:Authority", "https://demo.duendesoftware.com" },
                    { "Auth:Audience", "api" },
                    { "AuditSettings:Enabled", "false" }
                });
            });
        }).CreateClient();
    }

    // ────────────────────────────────────────────
    // Helper: unique feature code per test
    // ────────────────────────────────────────────

    private static string UniqueCode() =>
        $"TEST_{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    // ────────────────────────────────────────────
    // Seed helpers
    // ────────────────────────────────────────────

    private async Task SeedDevAdminUserAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(_dbFixture.ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO users (id, display_name, email, is_active)
            VALUES (@Id, 'System Admin', 'admin@rgtspace.com', TRUE)
            ON CONFLICT (email) WHERE is_deleted = FALSE DO NOTHING",
            new { Id = DevAdminId });
    }

    private async Task SeedTestClientAsync(Guid clientId)
    {
        await SeedDevAdminUserAsync();
        await using var conn = new Npgsql.NpgsqlConnection(_dbFixture.ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO clients (id, name, code, status, created_by, updated_by)
            VALUES (@Id, 'Test Client', @Code, 'Active',
                    '019ac92a-de20-7793-b8df-b88a87ea4e34',
                    '019ac92a-de20-7793-b8df-b88a87ea4e34')
            ON CONFLICT DO NOTHING",
            new { Id = clientId, Code = $"TC_{clientId.ToString()[..8].ToUpper()}" });
    }

    private async Task<Guid> CreateFeatureViaApiAsync(
        string code, string name = "Test Feature", bool isActive = true)
    {
        await SeedDevAdminUserAsync();
        var response = await _client.PostAsJsonAsync("/api/v1/features", new
        {
            code,
            name,
            description = "Test description",
            isActive,
            requiresClient = false
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFeatureResponse>();
        return result!.Id;
    }

    private sealed record CreateFeatureResponse(Guid Id, string Code, string Name);

    // ────────────────────────────────────────────
    // Deserialization helpers
    // ────────────────────────────────────────────

    private sealed record FeatureDto(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        bool IsActive,
        bool RequiresClient);

    private sealed record ClientSubscriptionDto(
        Guid Id, Guid ClientId, string ClientName, string ClientCode,
        Guid FeatureId, bool IsEnabled);

    private sealed record ClientFeatureDto(
        Guid Id, Guid ClientId, Guid FeatureId,
        string FeatureCode, string FeatureName,
        bool IsEnabled, bool FeatureIsActive);

    private sealed record UserOverrideDto(
        Guid Id, Guid UserId, string UserDisplayName, string UserEmail,
        Guid FeatureId, string OverrideState, string? Reason);

    private sealed record UserOverrideByUserDto(
        Guid Id, Guid UserId, Guid FeatureId,
        string FeatureCode, string FeatureName,
        string OverrideState, string? Reason);

    // ================================================================
    // GET /api/v1/features
    // ================================================================

    [Fact]
    public async Task GetAll_ShouldReturn200_WithFeatures()
    {
        // Arrange — create a feature so there is at least one
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code);

        // Act
        var response = await _client.GetAsync("/api/v1/features");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var features = await response.Content.ReadFromJsonAsync<List<FeatureDto>>();
        features.Should().NotBeNull();
        features!.Should().Contain(f => f.Id == featureId && f.Code == code);
    }

    // ================================================================
    // GET /api/v1/features/{id}
    // ================================================================

    [Fact]
    public async Task GetById_ShouldReturn200_WhenExists()
    {
        // Arrange
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code, "My Feature");

        // Act
        var response = await _client.GetAsync($"/api/v1/features/{featureId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var feature = await response.Content.ReadFromJsonAsync<FeatureDto>();
        feature.Should().NotBeNull();
        feature!.Id.Should().Be(featureId);
        feature.Code.Should().Be(code);
        feature.Name.Should().Be("My Feature");
        feature.IsActive.Should().BeTrue();
        feature.RequiresClient.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_ShouldReturn404_WhenNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/features/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ================================================================
    // POST /api/v1/features
    // ================================================================

    [Fact]
    public async Task Create_ShouldReturn201_WithNewId()
    {
        // Arrange
        await SeedDevAdminUserAsync();
        var code = UniqueCode();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/features", new
        {
            code,
            name = "Brand New Feature",
            description = "A test feature",
            isActive = true,
            requiresClient = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateFeatureResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(Guid.Empty);
        result.Code.Should().Be(code);
        result.Name.Should().Be("Brand New Feature");
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenDuplicateCode()
    {
        // Arrange — create the first feature
        var code = UniqueCode();
        await CreateFeatureViaApiAsync(code);

        // Act — attempt to create a second feature with the same code
        var response = await _client.PostAsJsonAsync("/api/v1/features", new
        {
            code,
            name = "Duplicate Feature",
            description = (string?)null,
            isActive = true,
            requiresClient = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenInvalidCode()
    {
        // Arrange
        await SeedDevAdminUserAsync();

        // Act — lowercase code violates ^[A-Z][A-Z0-9_]*$ pattern
        var response = await _client.PostAsJsonAsync("/api/v1/features", new
        {
            code = "lowercase_bad",
            name = "Invalid Feature",
            description = (string?)null,
            isActive = true,
            requiresClient = false
        });

        // Assert — FluentValidation returns 400 via the VALIDATION_ERROR path
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ================================================================
    // PUT /api/v1/features/{id}
    // ================================================================

    [Fact]
    public async Task Update_ShouldReturn204_WhenExists()
    {
        // Arrange
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code, "Original Name", isActive: true);

        // Act — update the feature
        var response = await _client.PutAsJsonAsync($"/api/v1/features/{featureId}", new
        {
            name = "Updated Name",
            description = "Updated description",
            isActive = false,
            requiresClient = true
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify via GET that changes persisted
        var getResponse = await _client.GetAsync($"/api/v1/features/{featureId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await getResponse.Content.ReadFromJsonAsync<FeatureDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
        updated.Description.Should().Be("Updated description");
        updated.IsActive.Should().BeFalse();
        updated.RequiresClient.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenNotFound()
    {
        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/features/{Guid.NewGuid()}", new
        {
            name = "Ghost Feature",
            description = (string?)null,
            isActive = true,
            requiresClient = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ================================================================
    // DELETE /api/v1/features/{id}
    // ================================================================

    [Fact]
    public async Task Delete_ShouldReturn204_WhenExists()
    {
        // Arrange
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/features/{featureId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft-deleted: GET should return 404
        var getResponse = await _client.GetAsync($"/api/v1/features/{featureId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ShouldReturn404_WhenAlreadyDeleted()
    {
        // Arrange — create and then delete
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code);
        var firstDelete = await _client.DeleteAsync($"/api/v1/features/{featureId}");
        firstDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — delete again
        var response = await _client.DeleteAsync($"/api/v1/features/{featureId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ================================================================
    // PUT /api/v1/clients/{clientId}/features/{featureId}
    // ================================================================

    [Fact]
    public async Task UpsertClientFeature_ShouldReturn204()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        await SeedTestClientAsync(clientId);

        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/clients/{clientId}/features/{featureId}",
            new { isEnabled = true });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ================================================================
    // POST /api/v1/users/{userId}/feature-overrides
    // ================================================================

    [Fact]
    public async Task SetUserOverride_ShouldReturn204()
    {
        // Arrange — dev admin user already exists as seed; create a feature
        var code = UniqueCode();
        await CreateFeatureViaApiAsync(code);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/users/{DevAdminId}/feature-overrides",
            new
            {
                featureCode = code,
                overrideState = "FORCE_ON",
                reason = "Integration test override"
            });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ================================================================
    // DELETE /api/v1/users/{userId}/feature-overrides/{featureCode}
    // ================================================================

    [Fact]
    public async Task ClearUserOverride_ShouldReturn204()
    {
        // Arrange — set an override first so there is something to clear
        var code = UniqueCode();
        await CreateFeatureViaApiAsync(code);

        var setResponse = await _client.PostAsJsonAsync(
            $"/api/v1/users/{DevAdminId}/feature-overrides",
            new
            {
                featureCode = code,
                overrideState = "FORCE_OFF",
                reason = "Will be cleared"
            });
        setResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await _client.DeleteAsync(
            $"/api/v1/users/{DevAdminId}/feature-overrides/{code}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ClearUserOverride_ShouldReturn404_WhenFeatureNotFound()
    {
        // Arrange — ensure admin user exists
        await SeedDevAdminUserAsync();

        // Act — attempt to clear an override for a nonexistent feature code
        var response = await _client.DeleteAsync(
            $"/api/v1/users/{DevAdminId}/feature-overrides/NONEXISTENT_FEAT_XYZ");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ================================================================
    // GET /api/v1/features/{featureId}/clients
    // ================================================================

    [Fact]
    public async Task GetClientSubscriptions_ShouldReturn200_WithSubscriptions()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        await SeedTestClientAsync(clientId);
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code);

        // Subscribe client to feature
        await _client.PutAsJsonAsync(
            $"/api/v1/clients/{clientId}/features/{featureId}",
            new { isEnabled = true });

        // Act
        var response = await _client.GetAsync($"/api/v1/features/{featureId}/clients");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var subs = await response.Content.ReadFromJsonAsync<List<ClientSubscriptionDto>>();
        subs.Should().NotBeNull();
        subs!.Should().Contain(s => s.ClientId == clientId && s.FeatureId == featureId && s.IsEnabled);
    }

    // ================================================================
    // GET /api/v1/clients/{clientId}/features
    // ================================================================

    [Fact]
    public async Task GetClientFeatures_ShouldReturn200_WithFeatures()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        await SeedTestClientAsync(clientId);
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code);

        await _client.PutAsJsonAsync(
            $"/api/v1/clients/{clientId}/features/{featureId}",
            new { isEnabled = true });

        // Act
        var response = await _client.GetAsync($"/api/v1/clients/{clientId}/features");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var features = await response.Content.ReadFromJsonAsync<List<ClientFeatureDto>>();
        features.Should().NotBeNull();
        features!.Should().Contain(f => f.FeatureId == featureId && f.FeatureCode == code && f.IsEnabled);
    }

    // ================================================================
    // GET /api/v1/features/{featureId}/user-overrides
    // ================================================================

    [Fact]
    public async Task GetFeatureUserOverrides_ShouldReturn200_WithOverrides()
    {
        // Arrange
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code);

        await _client.PostAsJsonAsync(
            $"/api/v1/users/{DevAdminId}/feature-overrides",
            new { featureCode = code, overrideState = "FORCE_ON", reason = "Testing overrides list" });

        // Act
        var response = await _client.GetAsync($"/api/v1/features/{featureId}/user-overrides");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var overrides = await response.Content.ReadFromJsonAsync<List<UserOverrideDto>>();
        overrides.Should().NotBeNull();
        overrides!.Should().Contain(o => o.UserId == DevAdminId && o.FeatureId == featureId && o.OverrideState == "FORCE_ON");
    }

    // ================================================================
    // GET /api/v1/users/{userId}/feature-overrides
    // ================================================================

    [Fact]
    public async Task GetUserOverrides_ShouldReturn200_WithOverrides()
    {
        // Arrange
        var code = UniqueCode();
        var featureId = await CreateFeatureViaApiAsync(code);

        await _client.PostAsJsonAsync(
            $"/api/v1/users/{DevAdminId}/feature-overrides",
            new { featureCode = code, overrideState = "FORCE_OFF", reason = "Testing user overrides list" });

        // Act
        var response = await _client.GetAsync($"/api/v1/users/{DevAdminId}/feature-overrides");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var overrides = await response.Content.ReadFromJsonAsync<List<UserOverrideByUserDto>>();
        overrides.Should().NotBeNull();
        overrides!.Should().Contain(o => o.FeatureId == featureId && o.FeatureCode == code && o.OverrideState == "FORCE_OFF");
    }

    // ────────────────────────────────────────────
    // Evaluation DTOs
    // ────────────────────────────────────────────

    private sealed record FeatureDecisionDto(
        bool IsEnabled, string ReasonCode, string FeatureCode);

    private sealed record BulkEvalResponseDto(
        Guid UserId, Guid? ClientId, DateTime EvaluatedAt,
        List<FeatureDecisionDto> Features);

    // ================================================================
    // GET /api/v1/features/evaluate/{featureCode} (Single Eval)
    // ================================================================

    [Fact]
    public async Task EvaluateSingle_ShouldReturn200_WithDecision()
    {
        // Arrange — create a system feature (no client required)
        var code = UniqueCode();
        await CreateFeatureViaApiAsync(code, "Eval Single Test", isActive: true);
        var userId = DevAdminId;

        // Act
        var response = await _client.GetAsync($"/api/v1/features/evaluate/{code}?userId={userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var decision = await response.Content.ReadFromJsonAsync<FeatureDecisionDto>();
        decision.Should().NotBeNull();
        decision!.FeatureCode.Should().Be(code);
        decision.IsEnabled.Should().BeTrue();
        decision.ReasonCode.Should().Be("GRANTED_SYSTEM");
    }

    [Fact]
    public async Task EvaluateSingle_ShouldReturn400_WhenUserIdMissing()
    {
        // Arrange — create a feature so the code is valid
        var code = UniqueCode();
        await CreateFeatureViaApiAsync(code, "Eval No UserId Test");

        // Act — call without userId query param
        var response = await _client.GetAsync($"/api/v1/features/evaluate/{code}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ================================================================
    // GET /api/v1/features/evaluate (Bulk Eval)
    // ================================================================

    [Fact]
    public async Task EvaluateBulk_ShouldReturn200_WithFeatures()
    {
        // Arrange — create a feature so there is at least one
        var code = UniqueCode();
        await CreateFeatureViaApiAsync(code, "Eval Bulk Test", isActive: true);
        var userId = DevAdminId;

        // Act
        var response = await _client.GetAsync($"/api/v1/features/evaluate?userId={userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BulkEvalResponseDto>();
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Features.Should().NotBeEmpty();
        result.Features.Should().Contain(f => f.FeatureCode == code);
    }

    [Fact]
    public async Task EvaluateBulk_ShouldReturn400_WhenUserIdMissing()
    {
        // Act — call without userId query param
        var response = await _client.GetAsync("/api/v1/features/evaluate");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
