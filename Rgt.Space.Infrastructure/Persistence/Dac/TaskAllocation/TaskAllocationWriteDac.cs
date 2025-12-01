using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.TaskAllocation;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Infrastructure.Resilience;

namespace Rgt.Space.Infrastructure.Persistence.Dac.TaskAllocation;

public sealed class TaskAllocationWriteDac : ITaskAllocationWriteDac
{
    private readonly ITenantConnectionFactory _connFactory;
    private readonly ITenantProvider _tenant;
    private readonly ResiliencePipelineRegistry<string> _pipelineRegistry;
    private readonly IOptions<ResilienceSettings> _resilienceSettings;
    private readonly ILogger<TaskAllocationWriteDac> _logger;

    public TaskAllocationWriteDac(
        ITenantConnectionFactory connFactory,
        ITenantProvider tenant,
        ResiliencePipelineRegistry<string> pipelineRegistry,
        IOptions<ResilienceSettings> resilienceSettings,
        ILogger<TaskAllocationWriteDac> logger)
    {
        _connFactory = connFactory;
        _tenant = tenant;
        _pipelineRegistry = pipelineRegistry;
        _resilienceSettings = resilienceSettings;
        _logger = logger;
    }

    private ResiliencePipeline GetPipeline()
    {
        var tenantId = _tenant.Id ?? "Global";
        var pipelineKey = tenantId;

        if (!_pipelineRegistry.TryGetPipeline(pipelineKey, out var pipeline))
        {
            _pipelineRegistry.TryAddBuilder(pipelineKey, (builder, context) =>
            {
                var settings = _resilienceSettings.Value.TenantDb;
                builder.AddPipelineFromSettings(
                    settings,
                    ResiliencePolicies.IsSqlTransientError,
                    $"TenantDb:{tenantId}",
                    _logger);
            });
            pipeline = _pipelineRegistry.GetPipeline(pipelineKey);
        }
        return pipeline;
    }

    public async Task<bool> AssignUserAsync(Guid projectId, Guid userId, string positionCode, Guid? assignedBy, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connString = await _connFactory.GetSqlConnectionStringAsync(string.Empty, ct);

        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            
            // Atomic Check: Only insert if User is Active
            const string sql = @"
                INSERT INTO project_assignments (
                    project_id, 
                    user_id, 
                    position_code, 
                    created_by, 
                    updated_by
                )
                SELECT 
                    @ProjectId, 
                    @UserId, 
                    @PositionCode, 
                    @By, 
                    @By
                FROM users u
                WHERE u.id = @UserId AND u.is_active = TRUE
                ON CONFLICT (project_id, user_id, position_code) WHERE is_deleted = FALSE
                DO NOTHING;
            ";

            var rows = await conn.ExecuteAsync(sql, new { ProjectId = projectId, UserId = userId, PositionCode = positionCode, By = assignedBy });
            return rows > 0;
        }, ct);
    }

    public async Task<bool> UnassignUserAsync(Guid projectId, Guid userId, string positionCode, Guid? unassignedBy, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connString = await _connFactory.GetSqlConnectionStringAsync(string.Empty, ct);

        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                UPDATE project_assignments
                SET 
                    is_deleted = TRUE, 
                    deleted_at = (now() AT TIME ZONE 'utc'), 
                    deleted_by = @By
                WHERE 
                    project_id = @ProjectId 
                    AND user_id = @UserId 
                    AND position_code = @PositionCode 
                    AND is_deleted = FALSE;
            ";

            var rows = await conn.ExecuteAsync(sql, new { ProjectId = projectId, UserId = userId, PositionCode = positionCode, By = unassignedBy });
            return rows > 0;
        }, ct);
    }

    public async Task<bool> UpdateAssignmentAsync(Guid projectId, Guid userId, string oldPositionCode, string newPositionCode, Guid? updatedBy, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connString = await _connFactory.GetSqlConnectionStringAsync(string.Empty, ct);

        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            await using var transaction = await conn.BeginTransactionAsync(token);

            try
            {
                // 1. Soft Delete Old Assignment
                const string deleteSql = @"
                    UPDATE project_assignments
                    SET 
                        is_deleted = TRUE, 
                        deleted_at = (now() AT TIME ZONE 'utc'), 
                        deleted_by = @By
                    WHERE 
                        project_id = @ProjectId 
                        AND user_id = @UserId 
                        AND position_code = @OldPositionCode 
                        AND is_deleted = FALSE;
                ";
                
                var deletedRows = await conn.ExecuteAsync(deleteSql, 
                    new { ProjectId = projectId, UserId = userId, OldPositionCode = oldPositionCode, By = updatedBy }, 
                    transaction);

                if (deletedRows == 0)
                {
                    // If we couldn't delete the old one, it means it didn't exist or was already deleted.
                    // We should abort the update to prevent creating a new assignment without removing the old one (if that was the intent).
                    // Or should we proceed? The requirement is "Update", which implies "Change".
                    // If old one is missing, it's not an update, it's a create.
                    // Let's return false to indicate "Old assignment not found".
                    await transaction.RollbackAsync(token);
                    return false;
                }

                // 2. Insert New Assignment (or reactivate)
                const string insertSql = @"
                    INSERT INTO project_assignments (
                        project_id, 
                        user_id, 
                        position_code, 
                        created_by, 
                        updated_by
                    )
                    SELECT 
                        @ProjectId, 
                        @UserId, 
                        @NewPositionCode, 
                        @By, 
                        @By
                    FROM users u
                    WHERE u.id = @UserId AND u.is_active = TRUE
                    ON CONFLICT (project_id, user_id, position_code) WHERE is_deleted = FALSE
                    DO NOTHING;
                ";

                var insertedRows = await conn.ExecuteAsync(insertSql, 
                    new { ProjectId = projectId, UserId = userId, NewPositionCode = newPositionCode, By = updatedBy }, 
                    transaction);

                await transaction.CommitAsync(token);
                return true; // Successful update
            }
            catch
            {
                await transaction.RollbackAsync(token);
                throw;
            }
        }, ct);
    }
}
