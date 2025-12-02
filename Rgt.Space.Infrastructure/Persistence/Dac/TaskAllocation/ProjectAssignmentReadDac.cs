using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.TaskAllocation;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Resilience;

namespace Rgt.Space.Infrastructure.Persistence.Dac.TaskAllocation;

public sealed class ProjectAssignmentReadDac : IProjectAssignmentReadDac
{
    private readonly ISystemConnectionFactory _systemConnFactory;
    private readonly ResiliencePipelineRegistry<string> _pipelineRegistry;
    private readonly IOptions<ResilienceSettings> _resilienceSettings;
    private readonly ILogger<ProjectAssignmentReadDac> _logger;

    public ProjectAssignmentReadDac(
        ISystemConnectionFactory systemConnFactory,
        ResiliencePipelineRegistry<string> pipelineRegistry,
        IOptions<ResilienceSettings> resilienceSettings,
        ILogger<ProjectAssignmentReadDac> logger)
    {
        _systemConnFactory = systemConnFactory;
        _pipelineRegistry = pipelineRegistry;
        _resilienceSettings = resilienceSettings;
        _logger = logger;
    }

    private ResiliencePipeline GetPipeline()
    {
        // Project Assignment reads are treated as system/global operations here
        const string pipelineKey = "System";
        
        if (!_pipelineRegistry.TryGetPipeline(pipelineKey, out var pipeline))
        {
            _pipelineRegistry.TryAddBuilder(pipelineKey, (builder, context) =>
            {
                var settings = _resilienceSettings.Value.MasterDb;
                builder.AddPipelineFromSettings(
                    settings,
                    ResiliencePolicies.IsSqlTransientError,
                    $"Db:{pipelineKey}",
                    _logger);
            });
            pipeline = _pipelineRegistry.GetPipeline(pipelineKey);
        }
        return pipeline;
    }

    public async Task<IReadOnlyList<ProjectAssignmentReadModel>> GetAllAsync(CancellationToken ct)
    {
        // "God View" - Global query across all projects
        var pipeline = GetPipeline();
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);

        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                SELECT 
                    p.id AS ProjectId,
                    p.name AS ProjectName,
                    p.code AS ProjectCode,
                    c.id AS ClientId,
                    c.name AS ClientName,
                    u.id AS UserId,
                    u.display_name AS UserName,
                    pa.position_code AS PositionCode
                FROM projects p
                JOIN clients c ON p.client_id = c.id
                JOIN project_assignments pa ON p.id = pa.project_id
                JOIN users u ON pa.user_id = u.id
                WHERE 
                    p.is_deleted = FALSE 
                    AND c.is_deleted = FALSE
                    AND pa.is_deleted = FALSE
                    AND u.is_active = TRUE -- Zombie Filter
                ORDER BY c.name, p.name, pa.position_code";

            var result = await conn.QueryAsync<ProjectAssignmentReadModel>(sql);
            return result.AsList();
        }, ct);
    }

    public async Task<IReadOnlyList<ProjectAssignmentReadModel>> GetByProjectIdAsync(Guid projectId, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);

        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                SELECT 
                    p.id AS ProjectId,
                    p.name AS ProjectName,
                    p.code AS ProjectCode,
                    c.id AS ClientId,
                    c.name AS ClientName,
                    u.id AS UserId,
                    u.display_name AS UserName,
                    pa.position_code AS PositionCode
                FROM projects p
                JOIN clients c ON p.client_id = c.id
                JOIN project_assignments pa ON p.id = pa.project_id
                JOIN users u ON pa.user_id = u.id
                WHERE 
                    p.id = @ProjectId
                    AND p.is_deleted = FALSE 
                    AND c.is_deleted = FALSE
                    AND pa.is_deleted = FALSE
                    AND u.is_active = TRUE -- Zombie Filter
                ORDER BY pa.position_code";

            _logger.LogInformation("Executing GetByProjectIdAsync for ProjectId: {ProjectId}", projectId);
            var result = await conn.QueryAsync<ProjectAssignmentReadModel>(sql, new { ProjectId = projectId });
            var list = result.AsList();
            _logger.LogInformation("GetByProjectIdAsync returned {Count} assignments", list.Count);
            return list;
        }, ct);
    }
}
