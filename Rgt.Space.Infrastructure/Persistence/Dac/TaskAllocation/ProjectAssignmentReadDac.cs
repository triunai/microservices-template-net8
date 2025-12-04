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
    public async Task<(IReadOnlyList<ProjectAssignmentReadModel> Items, int TotalCount)> GetMatrixAsync(
        int page, 
        int pageSize, 
        Guid? clientId, 
        string? search, 
        CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);

        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            
            // CTE Strategy:
            // 1. visible_projects: Filter projects based on criteria (and future scoping)
            // 2. total_count: Count total matching projects
            // 3. paged_projects: Apply pagination to project IDs
            // 4. Final Select: Join back to get details and assignments
            
            const string sql = @"
                WITH visible_projects AS (
                    SELECT p.id 
                    FROM projects p
                    JOIN clients c ON p.client_id = c.id
                    WHERE p.is_deleted = FALSE 
                      AND c.is_deleted = FALSE
                      AND (@ClientId IS NULL OR p.client_id = @ClientId)
                      AND (@Search IS NULL OR p.name ILIKE @Search OR c.name ILIKE @Search)
                ),
                total_count AS (
                    SELECT COUNT(*) AS cnt FROM visible_projects
                ),
                paged_projects AS (
                    SELECT p.id 
                    FROM visible_projects vp
                    JOIN projects p ON vp.id = p.id
                    JOIN clients c ON p.client_id = c.id
                    ORDER BY c.name, p.name -- Sort by Client Name then Project Name
                    OFFSET @Offset LIMIT @Limit
                )
                SELECT 
                    p.id AS ProjectId, 
                    p.name AS ProjectName, 
                    p.code AS ProjectCode,
                    c.id AS ClientId, 
                    c.name AS ClientName,
                    u.id AS UserId, 
                    u.display_name AS UserName, 
                    pa.position_code AS PositionCode,
                    tc.cnt AS TotalCount
                FROM paged_projects pp
                CROSS JOIN total_count tc
                JOIN projects p ON pp.id = p.id
                JOIN clients c ON p.client_id = c.id
                LEFT JOIN project_assignments pa ON p.id = pa.project_id AND pa.is_deleted = FALSE
                LEFT JOIN users u ON pa.user_id = u.id AND u.is_active = TRUE
                ORDER BY c.name, p.name, pa.position_code";

            var offset = (page - 1) * pageSize;
            var p = new
            {
                Offset = offset,
                Limit = pageSize,
                ClientId = clientId,
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%"
            };

            // We need to capture TotalCount from the first row (if any)
            int totalCount = 0;
            
            // Use a custom DTO for the raw query result to handle the extra TotalCount column
            var rawResults = await conn.QueryAsync<_MatrixRow>(sql, p);
            var list = rawResults.AsList();

            if (list.Count > 0)
            {
                totalCount = (int)list[0].TotalCount;
            }

            // Map back to ReadModel
            var readModels = list.Select(r => new ProjectAssignmentReadModel(
                r.ProjectId,
                r.ProjectName,
                r.ProjectCode,
                r.ClientId,
                r.ClientName,
                r.UserId,
                r.UserName,
                r.PositionCode
            )).ToList();

            return (readModels, totalCount);
        }, ct);
    }

    // Private DTO for raw query result including TotalCount
    private sealed record _MatrixRow(
        Guid ProjectId,
        string ProjectName,
        string ProjectCode,
        Guid ClientId,
        string ClientName,
        Guid? UserId,
        string? UserName,
        string? PositionCode,
        long TotalCount);
}
