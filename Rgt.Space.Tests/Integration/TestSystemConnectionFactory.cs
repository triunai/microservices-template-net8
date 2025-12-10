using Rgt.Space.Core.Abstractions.Tenancy;

namespace Rgt.Space.Tests.Integration;

public class TestSystemConnectionFactory : ISystemConnectionFactory, ITenantConnectionFactory
{
    private readonly string _connectionString;

    public TestSystemConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Task<string> GetConnectionStringAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_connectionString);
    }

    public Task<string> GetSqlConnectionStringAsync(string tenantId, CancellationToken ct = default)
    {
        return Task.FromResult(_connectionString);
    }
}
