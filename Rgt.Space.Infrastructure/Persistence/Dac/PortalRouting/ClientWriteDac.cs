using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Persistence.Dac.PortalRouting;

public sealed class ClientWriteDac : IClientWriteDac
{
    private readonly ISystemConnectionFactory _systemConnFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ClientWriteDac> _logger;

    public ClientWriteDac(
        ISystemConnectionFactory systemConnFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ClientWriteDac> logger)
    {
        _systemConnFactory = systemConnFactory;
        // PortalDb is the system database
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task CreateAsync(Guid id, string name, string code, string status, Guid createdBy, CancellationToken ct)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                INSERT INTO clients (id, name, code, status, created_by, updated_by) 
                VALUES (@Id, @Name, @Code, @Status, @CreatedBy, @CreatedBy)";

            try
            {
                await conn.ExecuteAsync(sql, new
                {
                    Id = id,
                    Name = name,
                    Code = code,
                    Status = status,
                    CreatedBy = createdBy
                });
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
            {
                // Check if it's the code constraint
                if (ex.ConstraintName?.Contains("code") == true)
                {
                    throw new ConflictException("ROUTING_CLIENT_CODE_EXISTS", $"Client code '{code}' already exists.");
                }
                throw;
            }
        }, ct);
    }

    public async Task UpdateAsync(Guid id, string name, string code, string status, Guid updatedBy, CancellationToken ct)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                UPDATE clients 
                SET name = @Name, code = @Code, status = @Status, updated_by = @UpdatedBy, updated_at = NOW() AT TIME ZONE 'utc'
                WHERE id = @Id AND is_deleted = FALSE";

            try
            {
                var rows = await conn.ExecuteAsync(sql, new
                {
                    Id = id,
                    Name = name,
                    Code = code,
                    Status = status,
                    UpdatedBy = updatedBy
                });
                
                // Note: We don't throw if rows == 0 because the command handler usually checks existence first.
                // But if we wanted to be strict, we could.
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
            {
                if (ex.ConstraintName?.Contains("code") == true)
                {
                    throw new ConflictException("ROUTING_CLIENT_CODE_EXISTS", $"Client code '{code}' already exists.");
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
            const string sql = @"
                UPDATE clients 
                SET is_deleted = TRUE, deleted_by = @DeletedBy, deleted_at = NOW() AT TIME ZONE 'utc'
                WHERE id = @Id AND is_deleted = FALSE";

            await conn.ExecuteAsync(sql, new { Id = id, DeletedBy = deletedBy });
        }, ct);
    }
}
