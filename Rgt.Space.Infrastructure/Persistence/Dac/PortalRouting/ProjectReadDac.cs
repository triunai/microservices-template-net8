using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Persistence.Dac.PortalRouting;

public sealed class ProjectReadDac : IProjectReadDac
{
    private readonly string _connectionString;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ProjectReadDac> _logger;

    public ProjectReadDac(
        IConfiguration configuration,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ProjectReadDac> logger)
    {
        _connectionString = configuration.GetConnectionString("PortalDb")
            ?? throw new InvalidOperationException("PortalDb connection string not found");
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task<ProjectReadModel?> GetByIdAsync(Guid projectId, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            const string sql = @"
                SELECT
                    p.id, p.client_id, c.name as client_name,
                    p.name, p.code, p.external_url, p.status,
                    p.created_at, p.updated_at
                FROM projects p
                JOIN clients c ON p.client_id = c.id
                WHERE p.id = @Id AND p.is_deleted = FALSE";

            var row = await conn.QuerySingleOrDefaultAsync<_ProjectRow>(sql, new { Id = projectId });
            if (row is null) return null;

            return new ProjectReadModel(
                row.id, row.client_id, row.client_name, row.name,
                row.code, row.external_url, row.status, row.created_at, row.updated_at);
        }, ct);
    }

    public async Task<IReadOnlyList<ProjectReadModel>> GetAllAsync(CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            const string sql = @"
                SELECT
                    p.id, p.client_id, c.name as client_name,
                    p.name, p.code, p.external_url, p.status,
                    p.created_at, p.updated_at
                FROM projects p
                JOIN clients c ON p.client_id = c.id
                WHERE p.is_deleted = FALSE
                ORDER BY c.name, p.name";

            var rows = await conn.QueryAsync<_ProjectRow>(sql);
            return rows.Select(r => new ProjectReadModel(
                r.id, r.client_id, r.client_name, r.name,
                r.code, r.external_url, r.status, r.created_at, r.updated_at))
                .ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<ProjectReadModel>> GetByClientIdAsync(Guid clientId, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            const string sql = @"
                SELECT
                    p.id, p.client_id, c.name as client_name,
                    p.name, p.code, p.external_url, p.status,
                    p.created_at, p.updated_at
                FROM projects p
                JOIN clients c ON p.client_id = c.id
                WHERE p.client_id = @ClientId AND p.is_deleted = FALSE
                ORDER BY p.name";

            var rows = await conn.QueryAsync<_ProjectRow>(sql, new { ClientId = clientId });
            return rows.Select(r => new ProjectReadModel(
                r.id, r.client_id, r.client_name, r.name,
                r.code, r.external_url, r.status, r.created_at, r.updated_at))
                .ToList();
        }, ct);
    }

    public async Task<ProjectReadModel?> GetByClientAndCodeAsync(Guid clientId, string code, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            const string sql = @"
                SELECT
                    p.id, p.client_id, c.name as client_name,
                    p.name, p.code, p.external_url, p.status,
                    p.created_at, p.updated_at
                FROM projects p
                JOIN clients c ON p.client_id = c.id
                WHERE p.client_id = @ClientId AND p.code = @Code AND p.is_deleted = FALSE";

            var row = await conn.QuerySingleOrDefaultAsync<_ProjectRow>(sql, new { ClientId = clientId, Code = code });
            if (row is null) return null;

            return new ProjectReadModel(
                row.id, row.client_id, row.client_name, row.name,
                row.code, row.external_url, row.status, row.created_at, row.updated_at);
        }, ct);
    }

    // Dapper row model (matches database snake_case columns)
    private sealed record _ProjectRow(
        Guid id,
        Guid client_id,
        string client_name,
        string name,
        string code,
        string? external_url,
        string status,
        DateTime created_at,
        DateTime updated_at);
}
