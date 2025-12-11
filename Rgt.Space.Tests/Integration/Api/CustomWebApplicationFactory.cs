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
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders(); // Reduce noise
        });
    }
}
