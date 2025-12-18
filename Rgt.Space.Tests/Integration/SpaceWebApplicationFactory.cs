using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Configuration;
using NSubstitute;

namespace Rgt.Space.Tests.Integration;

public class SpaceWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public TestDbFixture DbFixture { get; } = new TestDbFixture();

    public async Task InitializeAsync()
    {
        await DbFixture.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await DbFixture.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override database connection strings
            var inMemoryConfig = new Dictionary<string, string?>
            {
                {"ConnectionStrings:PortalDb", DbFixture.ConnectionString},
                {"ConnectionStrings:MasterDb", DbFixture.ConnectionString},
                {"ConnectionStrings:TenantDb", DbFixture.ConnectionString},
                // Disable Audit Logger to avoid deadlocks
                {"AuditSettings:Enabled", "false"},
                // Set fixed key for tests
                {"LocalAuth:SigningKey", "TestSigningKey_MustBe32CharsLong12345"},
                {"LocalAuth:Issuer", "rgt-space-portal"},
                {"LocalAuth:Audience", "rgt-space-portal-api"}
            };
            config.AddInMemoryCollection(inMemoryConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Replace ISystemConnectionFactory to use our test DB
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISystemConnectionFactory));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<ISystemConnectionFactory>(sp =>
            {
                var mock = Substitute.For<ISystemConnectionFactory>();
                mock.GetConnectionStringAsync(Arg.Any<CancellationToken>())
                    .Returns(DbFixture.ConnectionString);
                return mock;
            });

            // Also replace ITenantConnectionFactory
             var tenantDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITenantConnectionFactory));
            if (tenantDescriptor != null)
            {
                services.Remove(tenantDescriptor);
            }

            services.AddSingleton<ITenantConnectionFactory>(sp =>
            {
                var mock = Substitute.For<ITenantConnectionFactory>();
                mock.GetSqlConnectionStringAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(DbFixture.ConnectionString);
                return mock;
            });
        });
    }
}
