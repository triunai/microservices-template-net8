using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MicroservicesBase.Core.Abstractions.Tenancy;

namespace MicroservicesBase.API.HealthChecks;

/// <summary>
/// Health check for verifying connectivity to a specific tenant's database.
/// This is crucial for multi-tenant systems to ensure each tenant DB is accessible.
/// </summary>
public sealed class TenantDatabaseHealthCheck : IHealthCheck
{
    private readonly ITenantConnectionFactory _connectionFactory;
    private readonly string _tenantId;
    private readonly ILogger<TenantDatabaseHealthCheck> _logger;

    public TenantDatabaseHealthCheck(
        ITenantConnectionFactory connectionFactory,
        string tenantId,
        ILogger<TenantDatabaseHealthCheck> logger)
    {
        _connectionFactory = connectionFactory;
        _tenantId = tenantId;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get connection string for the tenant
            var connectionString = await _connectionFactory.GetSqlConnectionStringAsync(_tenantId, cancellationToken);

            // Attempt to open a connection to the tenant database
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Execute a simple query to verify DB is responsive
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            _logger.LogDebug("Health check passed for tenant {TenantId}", _tenantId);

            return HealthCheckResult.Healthy($"Tenant database '{_tenantId}' is accessible");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for tenant {TenantId}", _tenantId);

            return HealthCheckResult.Unhealthy(
                $"Tenant database '{_tenantId}' is not accessible",
                ex);
        }
    }
}

