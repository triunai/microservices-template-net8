using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using Dapper;

namespace Rgt.Space.Infrastructure.Persistence.Dac.PortalRouting;

public sealed class ClientProjectMappingReadDac : IClientProjectMappingReadDac
{
    private readonly string _connectionString;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ClientProjectMappingReadDac> _logger;

    public ClientProjectMappingReadDac(
        IConfiguration configuration,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ClientProjectMappingReadDac> logger)
    {
        _connectionString = configuration.GetConnectionString("PortalDb")
            ?? throw new InvalidOperationException("PortalDb connection string not found");
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task<ClientProjectMappingReadModel?> GetByIdAsync(Guid mappingId, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    m.id,
                    m.project_id,
                    p.name AS project_name,
                    p.code AS project_code,
                    p.client_id,
                    c.name AS client_name,
                    c.code AS client_code,
                    m.routing_url,
                    m.environment,
                    m.status,
                    m.created_at
                FROM client_project_mappings m
                INNER JOIN projects p ON m.project_id = p.id
                INNER JOIN clients c ON p.client_id = c.id
                WHERE m.id = @MappingId AND m.is_deleted = FALSE";

            var result = await conn.QuerySingleOrDefaultAsync<_MappingRow>(
                sql,
                new { MappingId = mappingId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            if (result is null) return null;

            return new ClientProjectMappingReadModel(
                result.id,
                result.project_id,
                result.project_name,
                result.project_code,
                result.client_id,
                result.client_name,
                result.client_code,
                result.routing_url,
                result.environment,
                result.status,
                result.created_at);
        }, ct);
    }

    public async Task<ClientProjectMappingReadModel?> GetByRoutingUrlAsync(string routingUrl, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    m.id,
                    m.project_id,
                    p.name AS project_name,
                    p.code AS project_code,
                    p.client_id,
                    c.name AS client_name,
                    c.code AS client_code,
                    m.routing_url,
                    m.environment,
                    m.status,
                    m.created_at
                FROM client_project_mappings m
                INNER JOIN projects p ON m.project_id = p.id
                INNER JOIN clients c ON p.client_id = c.id
                WHERE m.routing_url = @RoutingUrl AND m.is_deleted = FALSE";

            var result = await conn.QuerySingleOrDefaultAsync<_MappingRow>(
                sql,
                new { RoutingUrl = routingUrl },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            if (result is null) return null;

            return new ClientProjectMappingReadModel(
                result.id,
                result.project_id,
                result.project_name,
                result.project_code,
                result.client_id,
                result.client_name,
                result.client_code,
                result.routing_url,
                result.environment,
                result.status,
                result.created_at);
        }, ct);
    }

    public async Task<IReadOnlyList<ClientProjectMappingReadModel>> GetAllAsync(CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    m.id,
                    m.project_id,
                    p.name AS project_name,
                    p.code AS project_code,
                    p.client_id,
                    c.name AS client_name,
                    c.code AS client_code,
                    m.routing_url,
                    m.environment,
                    m.status,
                    m.created_at
                FROM client_project_mappings m
                INNER JOIN projects p ON m.project_id = p.id
                INNER JOIN clients c ON p.client_id = c.id
                WHERE m.is_deleted = FALSE
                ORDER BY c.name, p.name, m.environment";

            var results = await conn.QueryAsync<_MappingRow>(
                sql,
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            return results
                .Select(r => new ClientProjectMappingReadModel(
                    r.id,
                    r.project_id,
                    r.project_name,
                    r.project_code,
                    r.client_id,
                    r.client_name,
                    r.client_code,
                    r.routing_url,
                    r.environment,
                    r.status,
                    r.created_at))
                .ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<ClientProjectMappingReadModel>> GetByProjectIdAsync(Guid projectId, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    m.id,
                    m.project_id,
                    p.name AS project_name,
                    p.code AS project_code,
                    p.client_id,
                    c.name AS client_name,
                    c.code AS client_code,
                    m.routing_url,
                    m.environment,
                    m.status,
                    m.created_at
                FROM client_project_mappings m
                INNER JOIN projects p ON m.project_id = p.id
                INNER JOIN clients c ON p.client_id = c.id
                WHERE m.project_id = @ProjectId AND m.is_deleted = FALSE
                ORDER BY m.environment";

            var results = await conn.QueryAsync<_MappingRow>(
                sql,
                new { ProjectId = projectId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            return results
                .Select(r => new ClientProjectMappingReadModel(
                    r.id,
                    r.project_id,
                    r.project_name,
                    r.project_code,
                    r.client_id,
                    r.client_name,
                    r.client_code,
                    r.routing_url,
                    r.environment,
                    r.status,
                    r.created_at))
                .ToList();
        }, ct);
    }

    private sealed record _MappingRow(
        Guid id,
        Guid project_id,
        string project_name,
        string project_code,
        Guid client_id,
        string client_name,
        string client_code,
        string routing_url,
        string environment,
        string status,
        DateTime created_at);
}
