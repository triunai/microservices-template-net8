using Testcontainers.PostgreSql;

namespace Rgt.Space.Tests.Integration.Fixtures;

/// <summary>
/// Shared fixture for integration tests that require a database.
/// Supports both Testcontainers (local) and service container (CI).
/// </summary>
public class TestDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _container;

    /// <summary>
    /// Gets the connection string to the test database.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public TestDbFixture()
    {
        // Check if we are running in CI with a provided connection string
        var ciConnString = Environment.GetEnvironmentVariable("ConnectionStrings__TestDb");

        if (!string.IsNullOrWhiteSpace(ciConnString))
        {
            ConnectionString = ciConnString;
            _container = null;
        }
        else
        {
            // Use Testcontainers
            _container = new PostgreSqlBuilder()
                .WithImage("public.ecr.aws/docker/library/postgres:15-alpine")
                .WithDatabase("test_db")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
        }
    }

    public async Task InitializeAsync()
    {
        if (_container != null)
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        // Initialize Schema (Idempotent script execution)
        await TestDatabaseInitializer.InitializeAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
