using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Persistence.Dac.Identity;

public sealed class RoleReadDac : IRoleReadDac
{
    private readonly ISystemConnectionFactory _connFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<RoleReadDac> _logger;

    public RoleReadDac(
        ISystemConnectionFactory connFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<RoleReadDac> logger)
    {
        _connFactory = connFactory;
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task<RoleReadModel?> GetByIdAsync(Guid roleId, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                SELECT 
                    r.id,
                    r.name,
                    r.code,
                    r.description,
                    r.is_system,
                    r.is_active,
                    (SELECT COUNT(*) FROM user_roles ur WHERE ur.role_id = r.id) as user_count,
                    r.created_at,
                    r.created_by,
                    r.updated_at,
                    r.updated_by
                FROM roles r
                WHERE r.id = @RoleId";

            var row = await conn.QuerySingleOrDefaultAsync<_RoleRow>(sql, new { RoleId = roleId });
            return row?.ToReadModel();
        }, ct);
    }

    public async Task<RoleReadModel?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                SELECT 
                    r.id,
                    r.name,
                    r.code,
                    r.description,
                    r.is_system,
                    r.is_active,
                    (SELECT COUNT(*) FROM user_roles ur WHERE ur.role_id = r.id) as user_count,
                    r.created_at,
                    r.created_by,
                    r.updated_at,
                    r.updated_by
                FROM roles r
                WHERE r.code = @Code";

            var row = await conn.QuerySingleOrDefaultAsync<_RoleRow>(sql, new { Code = code });
            return row?.ToReadModel();
        }, ct);
    }

    public async Task<IReadOnlyList<RoleReadModel>> GetAllAsync(CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                SELECT 
                    r.id,
                    r.name,
                    r.code,
                    r.description,
                    r.is_system,
                    r.is_active,
                    (SELECT COUNT(*) FROM user_roles ur WHERE ur.role_id = r.id) as user_count,
                    r.created_at,
                    r.created_by,
                    r.updated_at,
                    r.updated_by
                FROM roles r
                ORDER BY r.is_system DESC, r.name ASC";

            var rows = await conn.QueryAsync<_RoleRow>(sql);
            return rows.Select(r => r.ToReadModel()).ToList();
        }, ct);
    }

    public async Task<int> GetUserCountAsync(Guid roleId, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = "SELECT COUNT(*) FROM user_roles WHERE role_id = @RoleId";
            return await conn.ExecuteScalarAsync<int>(sql, new { RoleId = roleId });
        }, ct);
    }

    public async Task<IReadOnlyList<UserRoleReadModel>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            const string sql = @"
                SELECT 
                    ur.id,
                    ur.role_id,
                    r.name as role_name,
                    r.code as role_code,
                    ur.assigned_at,
                    u.display_name as assigned_by_name
                FROM user_roles ur
                JOIN roles r ON ur.role_id = r.id
                LEFT JOIN users u ON ur.assigned_by_user_id = u.id
                WHERE ur.user_id = @UserId
                ORDER BY ur.assigned_at DESC";

            var rows = await conn.QueryAsync<_UserRoleRow>(sql, new { UserId = userId });
            return rows.Select(r => new UserRoleReadModel(
                r.id,
                r.role_id,
                r.role_name,
                r.role_code,
                r.assigned_at,
                r.assigned_by_name
            )).ToList();
        }, ct);
    }

    // Private Dapper row types
    private sealed class _RoleRow
    {
        public Guid id { get; set; }
        public string name { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string? description { get; set; }
        public bool is_system { get; set; }
        public bool is_active { get; set; }
        public int user_count { get; set; }
        public DateTime created_at { get; set; }
        public Guid? created_by { get; set; }
        public DateTime updated_at { get; set; }
        public Guid? updated_by { get; set; }

        public RoleReadModel ToReadModel() => new(
            id, name, code, description, is_system, is_active, user_count,
            created_at, created_by, updated_at, updated_by
        );
    }

    private sealed class _UserRoleRow
    {
        public Guid id { get; set; }
        public Guid role_id { get; set; }
        public string role_name { get; set; } = string.Empty;
        public string role_code { get; set; } = string.Empty;
        public DateTime assigned_at { get; set; }
        public string? assigned_by_name { get; set; }
    }
}
