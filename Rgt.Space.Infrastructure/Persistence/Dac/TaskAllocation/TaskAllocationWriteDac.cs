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
    private readonly ISystemConnectionFactory _systemConnFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<TaskAllocationWriteDac> _logger;

    public TaskAllocationWriteDac(
        ISystemConnectionFactory systemConnFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<TaskAllocationWriteDac> logger)
    {
        _systemConnFactory = systemConnFactory;
        // Standard Pattern A: Inject and use the pre-registered "System" pipeline
        _pipeline = pipelineProvider.GetPipeline("System");
        _logger = logger;
    }

    public async Task<bool> AssignUserAsync(Guid projectId, Guid userId, string positionCode, Guid? assignedBy, CancellationToken ct)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);

        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token); // Propagate cancellation to connection open
            
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

            var cmd = new CommandDefinition(sql, new { ProjectId = projectId, UserId = userId, PositionCode = positionCode, By = assignedBy }, cancellationToken: token);
            var rows = await conn.ExecuteAsync(cmd);
            return rows > 0;
        }, ct);
    }

    public async Task<bool> UnassignUserAsync(Guid projectId, Guid userId, string positionCode, Guid? unassignedBy, CancellationToken ct)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);

        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token); // Propagate cancellation to connection open
            
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

            var cmd = new CommandDefinition(sql, new { ProjectId = projectId, UserId = userId, PositionCode = positionCode, By = unassignedBy }, cancellationToken: token);
            var rows = await conn.ExecuteAsync(cmd);
            return rows > 0;
        }, ct);
    }

    public async Task<bool> UpdateAssignmentAsync(Guid projectId, Guid userId, string oldPositionCode, string newPositionCode, Guid? updatedBy, CancellationToken ct)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);

        return await _pipeline.ExecuteAsync(async token =>
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
                
                var deleteCmd = new CommandDefinition(deleteSql, 
                    new { ProjectId = projectId, UserId = userId, OldPositionCode = oldPositionCode, By = updatedBy }, 
                    transaction: transaction,
                    cancellationToken: token);
                var deletedRows = await conn.ExecuteAsync(deleteCmd);

                if (deletedRows == 0)
                {
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

                var insertCmd = new CommandDefinition(insertSql, 
                    new { ProjectId = projectId, UserId = userId, NewPositionCode = newPositionCode, By = updatedBy }, 
                    transaction: transaction,
                    cancellationToken: token);
                var insertedRows = await conn.ExecuteAsync(insertCmd);

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
