using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Infrastructure.Persistence.Dac.Identity;

public sealed class RoleWriteDac : IRoleWriteDac
{
    private readonly ISystemConnectionFactory _connFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<RoleWriteDac> _logger;

    public RoleWriteDac(
        ISystemConnectionFactory connFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<RoleWriteDac> logger)
    {
        _connFactory = connFactory;
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(Guid id, string name, string code, string? description, bool isActive, Guid createdBy, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                INSERT INTO roles (id, name, code, description, is_system, is_active, created_by, updated_by)
                VALUES (@Id, @Name, @Code, @Description, FALSE, @IsActive, @CreatedBy, @CreatedBy)
                RETURNING id";

            try
            {
                return await conn.ExecuteScalarAsync<Guid>(sql, new
                {
                    Id = id,
                    Name = name,
                    Code = code,
                    Description = description,
                    IsActive = isActive,
                    CreatedBy = createdBy
                });
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
            {
                if (ex.ConstraintName?.Contains("code") == true)
                {
                    throw new ConflictException(ErrorCatalog.ROLE_CODE_EXISTS, $"Role code '{code}' already exists.");
                }
                throw;
            }
        }, ct);
    }

    public async Task UpdateAsync(Guid id, string name, string? description, bool isActive, Guid updatedBy, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                UPDATE roles 
                SET name = @Name, 
                    description = @Description, 
                    is_active = @IsActive, 
                    updated_by = @UpdatedBy, 
                    updated_at = NOW() AT TIME ZONE 'utc'
                WHERE id = @Id";

            await conn.ExecuteAsync(sql, new
            {
                Id = id,
                Name = name,
                Description = description,
                IsActive = isActive,
                UpdatedBy = updatedBy
            });
        }, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            // Hard delete - roles don't need audit trail
            const string sql = "DELETE FROM roles WHERE id = @Id";
            await conn.ExecuteAsync(sql, new { Id = id });
        }, ct);
    }

    public async Task<Guid?> AssignRoleToUserAsync(Guid userId, Guid roleId, Guid assignedBy, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            
            // Generate ID using UUID v7
            var id = Uuid7.NewUuid7();
            
            const string sql = @"
                INSERT INTO user_roles (id, user_id, role_id, assigned_by_user_id, assigned_at)
                VALUES (@Id, @UserId, @RoleId, @AssignedBy, NOW() AT TIME ZONE 'utc')
                ON CONFLICT (user_id, role_id) DO NOTHING
                RETURNING id";

            var result = await conn.ExecuteScalarAsync<Guid?>(sql, new
            {
                Id = id,
                UserId = userId,
                RoleId = roleId,
                AssignedBy = assignedBy
            });

            return result;
        }, ct);
    }

    public async Task<bool> UnassignRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = "DELETE FROM user_roles WHERE user_id = @UserId AND role_id = @RoleId";
            var rows = await conn.ExecuteAsync(sql, new { UserId = userId, RoleId = roleId });
            return rows > 0;
        }, ct);
    }
}
