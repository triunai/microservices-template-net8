using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.PortalRouting;

namespace Rgt.Space.Infrastructure.Persistence.Dac.PortalRouting;

public sealed class ClientProjectMappingWriteDac : IClientProjectMappingWriteDac
{
    private readonly string _connectionString;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ClientProjectMappingWriteDac> _logger;

    public ClientProjectMappingWriteDac(
        IConfiguration configuration,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ClientProjectMappingWriteDac> logger)
    {
        _connectionString = configuration.GetConnectionString("PortalDb")
            ?? throw new InvalidOperationException("PortalDb connection string not found");
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(Guid projectId, string routingUrl, string environment, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(token);

            const string sql = @"
                INSERT INTO client_project_mappings
                    (id, project_id, routing_url, environment, status, created_at, updated_at, is_deleted)
                VALUES
                    (uuid_generate_v7(), @ProjectId, @RoutingUrl, @Environment, 'Active', now(), now(), false)
                RETURNING id";

            return await conn.ExecuteScalarAsync<Guid>(sql, new
            {
                ProjectId = projectId,
                RoutingUrl = routingUrl,
                Environment = environment
            });
        }, ct);
    }

    public async Task UpdateAsync(Guid id, string routingUrl, string environment, string status, CancellationToken ct)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(token);

            const string sql = @"
                UPDATE client_project_mappings
                SET
                    routing_url = @RoutingUrl,
                    environment = @Environment,
                    status = @Status,
                    updated_at = now()
                WHERE id = @Id AND is_deleted = FALSE";

            await conn.ExecuteAsync(sql, new
            {
                Id = id,
                RoutingUrl = routingUrl,
                Environment = environment,
                Status = status
            });
        }, ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(token);

            const string sql = @"
                UPDATE client_project_mappings
                SET
                    is_deleted = TRUE,
                    deleted_at = now(),
                    updated_at = now()
                WHERE id = @Id AND is_deleted = FALSE";

            await conn.ExecuteAsync(sql, new { Id = id });
        }, ct);
    }
}
