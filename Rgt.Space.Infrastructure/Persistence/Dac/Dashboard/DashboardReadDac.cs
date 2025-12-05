using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.Dashboard;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Contracts.Dashboard;
using Rgt.Space.Infrastructure.Resilience;

namespace Rgt.Space.Infrastructure.Persistence.Dac.Dashboard;

public sealed class DashboardReadDac : IDashboardReadDac
{
    private readonly ISystemConnectionFactory _connFactory;
    private readonly ResiliencePipelineRegistry<string> _pipelineRegistry;
    private readonly IOptions<ResilienceSettings> _resilienceSettings;
    private readonly ILogger<DashboardReadDac> _logger;

    public DashboardReadDac(
        ISystemConnectionFactory connFactory,
        ResiliencePipelineRegistry<string> pipelineRegistry,
        IOptions<ResilienceSettings> resilienceSettings,
        ILogger<DashboardReadDac> logger)
    {
        _connFactory = connFactory;
        _pipelineRegistry = pipelineRegistry;
        _resilienceSettings = resilienceSettings;
        _logger = logger;
    }

    private ResiliencePipeline GetPipeline()
    {
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

    public async Task<DashboardStatsResponse> GetStatsAsync(CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connString = await _connFactory.GetConnectionStringAsync(ct);

        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);

            // 1. KPI Metrics
            // Active Assignments: Count of non-deleted assignments
            // Active/Inactive Projects: Count based on status
            // Pending Vacancies: (ActiveProjects * 3 mandatory roles) - (Assignments for those roles)
            const string kpiSql = @"
                SELECT
                    (SELECT COUNT(*) FROM project_assignments WHERE is_deleted = FALSE) as ActiveAssignments,
                    (SELECT COUNT(*) FROM projects WHERE status = 'Active' AND is_deleted = FALSE) as ActiveProjects,
                    (SELECT COUNT(*) FROM projects WHERE status = 'Inactive' AND is_deleted = FALSE) as InactiveProjects,
                    
                    -- Calculate Vacancies: (ActiveProjects * 3) - (Filled Mandatory Roles)
                    (
                        (SELECT COUNT(*) * 3 FROM projects WHERE status = 'Active' AND is_deleted = FALSE)
                        -
                        (SELECT COUNT(*) 
                         FROM project_assignments pa
                         JOIN projects p ON pa.project_id = p.id
                         WHERE pa.position_code IN ('TECH_PIC', 'FUNC_PIC', 'SUPPORT_PIC')
                           AND pa.is_deleted = FALSE
                           AND p.status = 'Active'
                           AND p.is_deleted = FALSE)
                    ) as PendingVacancies";

            var kpis = await conn.QuerySingleAsync<DashboardKpis>(kpiSql);

            // 2. Assignment Distribution
            const string distSql = @"
                SELECT position_code as PositionCode, COUNT(*) as Count
                FROM project_assignments
                WHERE is_deleted = FALSE
                GROUP BY position_code
                ORDER BY Count DESC";

            var distribution = (await conn.QueryAsync<AssignmentDistribution>(distSql)).ToList();

            // 3. Top Vacancies (Projects missing mandatory roles)
            // This is a bit complex to do purely in SQL efficiently, 
            // but we can find projects missing specific roles using EXCEPT or NOT EXISTS
            const string vacancySql = @"
                WITH MandatoryRoles AS (
                    SELECT unnest(ARRAY['TECH_PIC', 'FUNC_PIC', 'SUPPORT_PIC']) as code
                ),
                ActiveProjects AS (
                    SELECT id, name FROM projects WHERE status = 'Active' AND is_deleted = FALSE
                ),
                ProjectRequirements AS (
                    SELECT p.id, p.name, mr.code
                    FROM ActiveProjects p
                    CROSS JOIN MandatoryRoles mr
                ),
                ExistingAssignments AS (
                    SELECT project_id, position_code
                    FROM project_assignments
                    WHERE is_deleted = FALSE
                )
                SELECT 
                    pr.id as ProjectId,
                    pr.name as ProjectName, 
                    pr.code as MissingPosition
                FROM ProjectRequirements pr
                LEFT JOIN ExistingAssignments ea 
                    ON pr.id = ea.project_id AND pr.code = ea.position_code
                WHERE ea.project_id IS NULL
                ORDER BY pr.name
                LIMIT 10";

            var vacancies = (await conn.QueryAsync<VacantPosition>(vacancySql)).ToList();

            return new DashboardStatsResponse
            {
                Kpis = kpis,
                AssignmentDistribution = distribution,
                TopVacancies = vacancies
            };

        }, ct);
    }
}
