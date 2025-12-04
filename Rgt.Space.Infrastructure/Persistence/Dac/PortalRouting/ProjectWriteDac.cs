using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Persistence.Dac.PortalRouting;

public sealed class ProjectWriteDac : IProjectWriteDac
{
    private readonly ISystemConnectionFactory _systemConnFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ProjectWriteDac> _logger;

    public ProjectWriteDac(
        ISystemConnectionFactory systemConnFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ProjectWriteDac> logger)
    {
        _systemConnFactory = systemConnFactory;
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task CreateAsync(Guid id, Guid clientId, string name, string code, string status, string? externalUrl, Guid createdBy, CancellationToken ct)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                INSERT INTO projects (id, client_id, name, code, status, external_url, created_by, updated_by) 
                VALUES (@Id, @ClientId, @Name, @Code, @Status, @ExternalUrl, @CreatedBy, @CreatedBy)";

            try
            {
                await conn.ExecuteAsync(sql, new
                {
                    Id = id,
                    ClientId = clientId,
                    Name = name,
                    Code = code,
                    Status = status,
                    ExternalUrl = externalUrl,
                    CreatedBy = createdBy
                });
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
            {
                if (ex.ConstraintName?.Contains("code") == true)
                {
                    throw new ConflictException("PROJECT_CODE_EXISTS_IN_CLIENT", $"Project code '{code}' already exists for this client.");
                }
                throw;
            }
        }, ct);
    }

    public async Task UpdateAsync(Guid id, string name, string code, string status, string? externalUrl, Guid updatedBy, CancellationToken ct)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                UPDATE projects 
                SET name = @Name, code = @Code, status = @Status, external_url = @ExternalUrl, updated_by = @UpdatedBy, updated_at = NOW() AT TIME ZONE 'utc'
                WHERE id = @Id AND is_deleted = FALSE";

            try
            {
                await conn.ExecuteAsync(sql, new
                {
                    Id = id,
                    Name = name,
                    Code = code,
                    Status = status,
                    ExternalUrl = externalUrl,
                    UpdatedBy = updatedBy
                });
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
            {
                if (ex.ConstraintName?.Contains("code") == true)
                {
                    throw new ConflictException("PROJECT_CODE_EXISTS_IN_CLIENT", $"Project code '{code}' already exists for this client.");
                }
                throw;
            }
        }, ct);
    }


    public async Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            await using var transaction = await conn.BeginTransactionAsync(token);

            try
            {
                // 1. Soft delete project
                const string deleteProjectSql = @"
                    UPDATE projects 
                    SET is_deleted = TRUE, deleted_by = @DeletedBy, deleted_at = NOW() AT TIME ZONE 'utc'
                    WHERE id = @Id AND is_deleted = FALSE";
                
                await conn.ExecuteAsync(deleteProjectSql, new { Id = id, DeletedBy = deletedBy }, transaction);

                // 2. Cascade soft delete to mappings
                const string deleteMappingsSql = @"
                    UPDATE client_project_mappings 
                    SET is_deleted = TRUE, deleted_by = @DeletedBy, deleted_at = NOW() AT TIME ZONE 'utc'
                    WHERE project_id = @Id AND is_deleted = FALSE";
                
                await conn.ExecuteAsync(deleteMappingsSql, new { Id = id, DeletedBy = deletedBy }, transaction);

                // 3. Cascade soft delete to assignments
                const string deleteAssignmentsSql = @"
                    UPDATE project_assignments 
                    SET is_deleted = TRUE, deleted_by = @DeletedBy, deleted_at = NOW() AT TIME ZONE 'utc'
                    WHERE project_id = @Id AND is_deleted = FALSE";

                await conn.ExecuteAsync(deleteAssignmentsSql, new { Id = id, DeletedBy = deletedBy }, transaction);

                await transaction.CommitAsync(token);
            }
            catch
            {
                await transaction.RollbackAsync(token);
                throw;
            }
        }, ct);
    }
}
