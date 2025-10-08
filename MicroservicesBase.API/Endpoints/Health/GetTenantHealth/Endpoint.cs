using FastEndpoints;
using Microsoft.Data.SqlClient;
using MicroservicesBase.Core.Abstractions.Tenancy;

namespace MicroservicesBase.API.Endpoints.Health.GetTenantHealth;

/// <summary>
/// Health check endpoint for a specific tenant's database.
/// Useful for admin dashboards to monitor tenant-specific connectivity.
/// </summary>
public sealed class Endpoint : Endpoint<Request, Response>
{
    private readonly ITenantConnectionFactory _connectionFactory;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(ITenantConnectionFactory connectionFactory, ILogger<Endpoint> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/health/tenant/{tenantName}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Check health of a specific tenant's database";
            s.Description = "Verifies connectivity and responsiveness of a tenant's database. Use tenant name (e.g., '7ELEVEN', 'BURGERKING'), not GUID.";
            s.Response<Response>(200, "Tenant database is healthy");
            s.Response<Response>(503, "Tenant database is unhealthy or unreachable");
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Get connection string for the tenant
            var connectionString = await _connectionFactory.GetSqlConnectionStringAsync(req.TenantName, ct);

            // Attempt to open a connection and execute a simple query
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(ct);

            var duration = DateTimeOffset.UtcNow - startTime;

            _logger.LogInformation("Health check passed for tenant {TenantName} in {Duration}ms", 
                req.TenantName, duration.TotalMilliseconds);

            await Send.OkAsync(new Response
            {
                Status = "Healthy",
                TenantName = req.TenantName,
                Message = $"Tenant database '{req.TenantName}' is accessible",
                Duration = duration.TotalMilliseconds,
                Timestamp = DateTimeOffset.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;

            _logger.LogWarning(ex, "Health check failed for tenant {TenantName}", req.TenantName);

            await Send.ResponseAsync(new Response
            {
                Status = "Unhealthy",
                TenantName = req.TenantName,
                Message = $"Tenant database '{req.TenantName}' is not accessible: {ex.Message}",
                Duration = duration.TotalMilliseconds,
                Timestamp = DateTimeOffset.UtcNow
            }, 503, ct);
        }
    }
}

