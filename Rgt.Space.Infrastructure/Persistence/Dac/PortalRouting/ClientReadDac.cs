using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Persistence.Dac.PortalRouting;

public sealed class ClientReadDac : IClientReadDac
{
    private readonly string _connectionString;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ClientReadDac> _logger;

    public ClientReadDac(
        IConfiguration configuration,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ClientReadDac> logger)
    {
        _connectionString = configuration.GetConnectionString("PortalDb")
            ?? throw new InvalidOperationException("PortalDb connection string not found");
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task<ClientReadModel?> GetByIdAsync(Guid clientId, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            const string sql = @"
                SELECT id, name, code, status, created_at, updated_at
                FROM clients
                WHERE id = @Id AND is_deleted = FALSE";

            var row = await conn.QuerySingleOrDefaultAsync<_ClientRow>(sql, new { Id = clientId });
            if (row is null) return null;

            return new ClientReadModel(row.id, row.name, row.code, row.status, row.created_at, row.updated_at);
        }, ct);
    }

    public async Task<ClientReadModel?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            const string sql = @"
                SELECT id, name, code, status, created_at, updated_at
                FROM clients
                WHERE code = @Code AND is_deleted = FALSE";

            var row = await conn.QuerySingleOrDefaultAsync<_ClientRow>(sql, new { Code = code });
            if (row is null) return null;

            return new ClientReadModel(row.id, row.name, row.code, row.status, row.created_at, row.updated_at);
        }, ct);
    }

    public async Task<IReadOnlyList<ClientReadModel>> GetAllAsync(CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            const string sql = @"
                SELECT id, name, code, status, created_at, updated_at
                FROM clients
                WHERE is_deleted = FALSE
                ORDER BY name";

            var rows = await conn.QueryAsync<_ClientRow>(sql);
            return rows.Select(r => new ClientReadModel(r.id, r.name, r.code, r.status, r.created_at, r.updated_at))
                .ToList();
        }, ct);
    }

    // Dapper row model (matches database snake_case columns)
    private sealed record _ClientRow(
        Guid id,
        string name,
        string code,
        string status,
        DateTime created_at,
        DateTime updated_at);
}
