using System.Net;
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
public class TenantResolutionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TenantResolutionTests(CustomWebApplicationFactory factory, TestDbFixture dbFixture)
    {
        // Reset static state to defaults before building the client
        ConfigurableTestAuthHandler.TenantId = "TEST_TENANT";
        ConfigurableTestAuthHandler.IsAuthenticated = true;

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISystemConnectionFactory>();
                services.AddSingleton<ISystemConnectionFactory>(
                    new TestSystemConnectionFactory(dbFixture.ConnectionString));

                services.RemoveAll<ITenantConnectionFactory>();
                services.AddSingleton<ITenantConnectionFactory>(
                    new TestSystemConnectionFactory(dbFixture.ConnectionString));

                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser, DevCurrentUser>();

                // Use ConfigurableTestAuthHandler for per-test tid control
                services.AddAuthentication(ConfigurableTestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, ConfigurableTestAuthHandler>(
                        ConfigurableTestAuthHandler.SchemeName, null);
                services.PostConfigure<AuthenticationOptions>(o =>
                {
                    o.DefaultAuthenticateScheme = ConfigurableTestAuthHandler.SchemeName;
                    o.DefaultChallengeScheme = ConfigurableTestAuthHandler.SchemeName;
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
    // Helper
    // ────────────────────────────────────────────

    private void ResetAuthState(string? tenantId = "TEST_TENANT", bool isAuthenticated = true)
    {
        ConfigurableTestAuthHandler.TenantId = tenantId;
        ConfigurableTestAuthHandler.IsAuthenticated = isAuthenticated;
    }

    private HttpRequestMessage CreateGetRequest(string url, string? xTenantHeader = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (xTenantHeader != null)
            request.Headers.Add("X-Tenant", xTenantHeader);
        return request;
    }

    // ────────────────────────────────────────────
    // Fix 1: Tenant Case-Insensitivity (4 tests)
    // ────────────────────────────────────────────

    [Fact]
    public async Task Request_ShouldSucceed_WhenTenantHeaderMatchesJwtTidCaseInsensitive()
    {
        // Arrange — JWT tid = "TEST_TENANT", header = "test_tenant" (lowercase)
        ResetAuthState(tenantId: "TEST_TENANT");
        var request = CreateGetRequest("/api/v1/features", xTenantHeader: "test_tenant");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — middleware compares OrdinalIgnoreCase, so this should NOT be 403
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "case-insensitive comparison should treat 'test_tenant' == 'TEST_TENANT'");
    }

    [Fact]
    public async Task Request_ShouldSucceed_WhenTenantHeaderMatchesJwtTidExactCase()
    {
        // Arrange — JWT tid = "TEST_TENANT", header = "TEST_TENANT" (exact match)
        ResetAuthState(tenantId: "TEST_TENANT");
        var request = CreateGetRequest("/api/v1/features", xTenantHeader: "TEST_TENANT");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — exact match, should not be 403
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "exact case match should always succeed");
    }

    [Fact]
    public async Task Request_Should403_WhenTenantHeaderConflictsWithJwtTid()
    {
        // Arrange — JWT tid = "TEST_TENANT", header = "DIFFERENT_TENANT" (mismatch)
        ResetAuthState(tenantId: "TEST_TENANT");
        var request = CreateGetRequest("/api/v1/features", xTenantHeader: "DIFFERENT_TENANT");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — middleware should reject with 403 (tenant spoofing attempt)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "mismatched JWT tid and X-Tenant header should result in 403");
    }

    [Fact]
    public async Task Request_ShouldSucceed_WhenNoTenantHeaderPresent()
    {
        // Arrange — JWT tid = "TEST_TENANT", no X-Tenant header
        ResetAuthState(tenantId: "TEST_TENANT");
        var request = CreateGetRequest("/api/v1/features");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — no header means no conflict, should not be 403
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "absence of X-Tenant header means no conflict with JWT tid");
    }

    // ────────────────────────────────────────────
    // Fix 1: Tenant Normalization (2 tests)
    // ────────────────────────────────────────────

    [Fact]
    public async Task TenantCode_ShouldBeUppercaseInvariant_WhenResolvedFromJwt()
    {
        // Arrange — JWT tid in mixed case, no X-Tenant header
        ResetAuthState(tenantId: "mixed_Case_Tenant");
        var request = CreateGetRequest("/api/v1/features");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — middleware normalizes to uppercase, pipeline should work (not 403)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "mixed-case JWT tid should be normalized to uppercase, not rejected");
    }

    [Fact]
    public async Task TenantCode_ShouldMatchCaseInsensitive_WhenHeaderAndJwtDifferInCase()
    {
        // Arrange — JWT tid = "lower_case", header = "LOWER_CASE"
        // Middleware compares OrdinalIgnoreCase, so these should match
        ResetAuthState(tenantId: "lower_case");
        var request = CreateGetRequest("/api/v1/features", xTenantHeader: "LOWER_CASE");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — case-insensitive comparison means no conflict
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "case-insensitive comparison should treat 'lower_case' == 'LOWER_CASE'");
    }

    // ────────────────────────────────────────────
    // Fix 8: TestAuthHandler tid Claim (2 tests)
    // ────────────────────────────────────────────

    [Fact]
    public async Task AuthenticatedRequest_ShouldResolveTenantFromJwtTid()
    {
        // Arrange — authenticated with JWT tid, no X-Tenant header
        ResetAuthState(tenantId: "TEST_TENANT");
        var request = CreateGetRequest("/api/v1/features");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — proves tid claim is being used for tenant resolution (200 OK from features endpoint)
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated request with tid claim should resolve tenant and reach the endpoint");
    }

    [Fact]
    public async Task AuthenticatedRequest_WithMatchingXTenantHeader_ShouldSucceed()
    {
        // Arrange — authenticated with JWT tid + matching X-Tenant header
        ResetAuthState(tenantId: "TEST_TENANT");
        var request = CreateGetRequest("/api/v1/features", xTenantHeader: "TEST_TENANT");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — matching header confirms no conflict, should reach endpoint
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated request with matching X-Tenant header should succeed");
    }
}
