using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Rgt.Space.Core.Abstractions.Tenancy;
using Testcontainers.PostgreSql;

namespace Rgt.Space.Tests.Integration.Api;

/// <summary>
/// Custom WebApplicationFactory that spins up a test database container
/// and configures the API to use it.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public CustomWebApplicationFactory()
    {
        // Use the same image as Integration Tests
        _postgres = new PostgreSqlBuilder()
            .WithImage("public.ecr.aws/docker/library/postgres:15-alpine")
            .WithDatabase("portal_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        // Note: we can't easily get the connection string until we start the container.
        // But the constructor must return.
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Initialize Schema
        await TestDatabaseInitializer.InitializeAsync(_postgres.GetConnectionString());
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing IDbConnection or ConnectionFactory registrations if any
            services.RemoveAll<ISystemConnectionFactory>();

            // Register our Test Connection Factory
            services.AddSingleton<ISystemConnectionFactory>(new TestSystemConnectionFactory(_postgres.GetConnectionString()));

            // Also need to override the Configuration "PortalDb" connection string because
            // the API might use it for HealthChecks or other services directly.
            // However, Configuration is usually built before ConfigureServices.
            // We can use ConfigureAppConfiguration.
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:PortalDb", _postgres.GetConnectionString() },
                { "ConnectionStrings:Redis", "localhost:6379" }, // Mock or ignore Redis
                { "Auth:Authority", "https://demo.duendesoftware.com" }, // Fake Auth
                { "Auth:Audience", "api" }
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders(); // Reduce noise
        });
    }
}
