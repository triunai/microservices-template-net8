using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Resilience;
using Npgsql;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using Dapper;

namespace Rgt.Space.Infrastructure.Persistence.Dac.Identity;

public sealed class UserReadDac : IUserReadDac
{
    private readonly ISystemConnectionFactory _connFactory;
    private readonly ResiliencePipelineRegistry<string> _pipelineRegistry;
    private readonly IOptions<ResilienceSettings> _resilienceSettings;
    private readonly ILogger<UserReadDac> _logger;

    public UserReadDac(
        ISystemConnectionFactory connFactory,
        ResiliencePipelineRegistry<string> pipelineRegistry,
        IOptions<ResilienceSettings> resilienceSettings,
        ILogger<UserReadDac> logger)
    {
        _connFactory = connFactory;
        _pipelineRegistry = pipelineRegistry;
        _resilienceSettings = resilienceSettings;
        _logger = logger;
    }

    private ResiliencePipeline GetPipeline()
    {
        // Use a static key for system/global queries
        const string pipelineKey = "System";
        
        if (!_pipelineRegistry.TryGetPipeline(pipelineKey, out var pipeline))
        {
            _pipelineRegistry.TryAddBuilder(pipelineKey, (builder, context) =>
            {
                // Use MasterDb settings for system queries as they are critical
                var settings = _resilienceSettings.Value.MasterDb;
                builder.AddPipelineFromSettings(
                    settings,
                    ResiliencePolicies.IsSqlTransientError,
                    $"SystemDb",
                    _logger);
            });
            pipeline = _pipelineRegistry.GetPipeline(pipelineKey);
        }
        return pipeline;
    }

    public async Task<UserReadModel?> GetByIdAsync(Guid userId, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        
        _logger.LogDebug("Querying user {UserId} (System)", userId);
        
        var connectionString = await _connFactory.GetConnectionStringAsync(ct);
        
        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    id,
                    display_name,
                    email,
                    contact_number,
                    is_active,
                    local_login_enabled,
                    sso_login_enabled,
                    sso_provider,
                    external_id,
                    last_login_at,
                    last_login_provider,
                    created_at,
                    created_by,
                    updated_at,
                    updated_by
                FROM users
                WHERE id = @UserId AND is_deleted = FALSE";

            var result = await conn.QuerySingleOrDefaultAsync<_UserRow>(
                sql,
                new { UserId = userId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            if (result is null)
            {
                _logger.LogDebug("User {UserId} not found (System)", userId);
                return null;
            }

            return new UserReadModel(
                result.id,
                result.display_name,
                result.email,
                result.contact_number,
                result.is_active,
                result.local_login_enabled,
                result.sso_login_enabled,
                result.sso_provider,
                result.external_id,
                result.last_login_at,
                result.last_login_provider,
                result.created_at,
                result.created_by,
                result.updated_at,
                result.updated_by);
        }, ct);
    }

    public async Task<UserReadModel?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connectionString = await _connFactory.GetConnectionStringAsync(ct);
        
        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    id,
                    display_name,
                    email,
                    contact_number,
                    is_active,
                    local_login_enabled,
                    sso_login_enabled,
                    sso_provider,
                    external_id,
                    last_login_at,
                    last_login_provider,
                    created_at,
                    created_by,
                    updated_at,
                    updated_by
                FROM users
                WHERE email = @Email AND is_deleted = FALSE";

            var result = await conn.QuerySingleOrDefaultAsync<_UserRow>(
                sql,
                new { Email = email },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            if (result is null) return null;

            return new UserReadModel(
                result.id,
                result.display_name,
                result.email,
                result.contact_number,
                result.is_active,
                result.local_login_enabled,
                result.sso_login_enabled,
                result.sso_provider,
                result.external_id,
                result.last_login_at,
                result.last_login_provider,
                result.created_at,
                result.created_by,
                result.updated_at,
                result.updated_by);
        }, ct);
    }

    public async Task<UserReadModel?> GetByEmailAnyAsync(string email, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connectionString = await _connFactory.GetConnectionStringAsync(ct);
        
        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    id,
                    display_name,
                    email,
                    contact_number,
                    is_active,
                    local_login_enabled,
                    sso_login_enabled,
                    sso_provider,
                    external_id,
                    last_login_at,
                    last_login_provider,
                    created_at,
                    created_by,
                    updated_at,
                    updated_by
                FROM users
                WHERE email = @Email"; // Intentionally ignoring is_deleted

            var result = await conn.QuerySingleOrDefaultAsync<_UserRow>(
                sql,
                new { Email = email },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            if (result is null) return null;

            return new UserReadModel(
                result.id,
                result.display_name,
                result.email,
                result.contact_number,
                result.is_active,
                result.local_login_enabled,
                result.sso_login_enabled,
                result.sso_provider,
                result.external_id,
                result.last_login_at,
                result.last_login_provider,
                result.created_at,
                result.created_by,
                result.updated_at,
                result.updated_by);
        }, ct);
    }

    public async Task<UserReadModel?> GetByExternalIdAsync(string provider, string externalId, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connectionString = await _connFactory.GetConnectionStringAsync(ct);
        
        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    id,
                    display_name,
                    email,
                    contact_number,
                    is_active,
                    local_login_enabled,
                    sso_login_enabled,
                    sso_provider,
                    external_id,
                    last_login_at,
                    last_login_provider,
                    created_at,
                    created_by,
                    updated_at,
                    updated_by
                FROM users
                WHERE sso_provider = @Provider
                  AND external_id = @ExternalId
                  AND is_deleted = FALSE";

            var result = await conn.QuerySingleOrDefaultAsync<_UserRow>(
                sql,
                new { Provider = provider, ExternalId = externalId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            if (result is null) return null;

            return new UserReadModel(
                result.id,
                result.display_name,
                result.email,
                result.contact_number,
                result.is_active,
                result.local_login_enabled,
                result.sso_login_enabled,
                result.sso_provider,
                result.external_id,
                result.last_login_at,
                result.last_login_provider,
                result.created_at,
                result.created_by,
                result.updated_at,
                result.updated_by);
        }, ct);
    }

    public async Task<IReadOnlyList<UserReadModel>> GetAllAsync(CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connectionString = await _connFactory.GetConnectionStringAsync(ct);
        
        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    id,
                    display_name,
                    email,
                    contact_number,
                    is_active,
                    local_login_enabled,
                    sso_login_enabled,
                    sso_provider,
                    external_id,
                    last_login_at,
                    last_login_provider,
                    created_at,

                    created_by,
                    updated_at,
                    updated_by
                FROM users
                WHERE is_deleted = FALSE
                ORDER BY display_name";

            var results = await conn.QueryAsync<_UserRow>(
                sql,
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            return results
                .Select(r => new UserReadModel(
                    r.id,
                    r.display_name,
                    r.email,
                    r.contact_number,
                    r.is_active,
                    r.local_login_enabled,
                    r.sso_login_enabled,
                    r.sso_provider,
                    r.external_id,
                    r.last_login_at,
                    r.last_login_provider,
                    r.created_at,
                    r.created_by,
                    r.updated_at,
                    r.updated_by))
                .ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<UserReadModel>> SearchAsync(string searchTerm, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connectionString = await _connFactory.GetConnectionStringAsync(ct);
        
        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                SELECT
                    id,
                    display_name,
                    email,
                    contact_number,
                    is_active,
                    local_login_enabled,
                    sso_login_enabled,
                    sso_provider,
                    external_id,
                    last_login_at,
                    last_login_provider,
                    created_at,
                    created_by,
                    updated_at,
                    updated_by
                FROM users
                WHERE is_deleted = FALSE
                  AND (display_name ILIKE @SearchTerm OR email ILIKE @SearchTerm)
                ORDER BY display_name
                LIMIT 20";

            var results = await conn.QueryAsync<_UserRow>(
                sql,
                new { SearchTerm = $"%{searchTerm}%" },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            return results
                .Select(r => new UserReadModel(
                    r.id,
                    r.display_name,
                    r.email,
                    r.contact_number,
                    r.is_active,
                    r.local_login_enabled,
                    r.sso_login_enabled,
                    r.sso_provider,
                    r.external_id,
                    r.last_login_at,
                    r.last_login_provider,
                    r.created_at,
                    r.created_by,
                    r.updated_at,
                    r.updated_by))
                .ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<UserPermissionReadModel>> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var pipeline = GetPipeline();
        var connectionString = await _connFactory.GetConnectionStringAsync(ct);
        
        return await pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(token);

            var sql = @"
                WITH effective_permissions AS (
                    -- 1. Role Permissions
                    SELECT rp.permission_id
                    FROM user_roles ur
                    JOIN role_permissions rp ON ur.role_id = rp.role_id
                    JOIN roles r ON ur.role_id = r.id
                    WHERE ur.user_id = @UserId
                      AND r.is_active = TRUE

                    UNION

                    -- 2. Overrides (Allow)
                    SELECT permission_id
                    FROM user_permission_overrides
                    WHERE user_id = @UserId
                      AND is_allowed = TRUE

                    EXCEPT

                    -- 3. Overrides (Deny)
                    SELECT permission_id
                    FROM user_permission_overrides
                    WHERE user_id = @UserId
                      AND is_allowed = FALSE
                )
                SELECT
                    m.code as module,
                    r.code as sub_module,
                    BOOL_OR(CASE WHEN a.code = 'VIEW' THEN TRUE ELSE FALSE END) as can_view,
                    BOOL_OR(CASE WHEN a.code = 'INSERT' THEN TRUE ELSE FALSE END) as can_insert,
                    BOOL_OR(CASE WHEN a.code = 'EDIT' THEN TRUE ELSE FALSE END) as can_edit,
                    BOOL_OR(CASE WHEN a.code = 'DELETE' THEN TRUE ELSE FALSE END) as can_delete
                FROM effective_permissions ep
                JOIN permissions p ON ep.permission_id = p.id
                JOIN resources r ON p.resource_id = r.id
                JOIN modules m ON r.module_id = m.id
                JOIN actions a ON p.action_id = a.id
                GROUP BY m.code, r.code
                ORDER BY m.code, r.code";

            var results = await conn.QueryAsync<_UserPermissionRow>(
                sql,
                new { UserId = userId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            return results
                .Select(r => new UserPermissionReadModel(
                    r.module,
                    r.sub_module,
                    r.can_view,
                    r.can_insert,
                    r.can_edit,
                    r.can_delete))
                .ToList();
        }, ct);
    }

    // Dapper row model
    private sealed record _UserRow(
        Guid id,
        string display_name,
        string email,
        string? contact_number,
        bool is_active,
        bool local_login_enabled,
        bool sso_login_enabled,
        string? sso_provider,
        string? external_id,
        DateTime? last_login_at,
        string? last_login_provider,
        DateTime created_at,
        Guid? created_by,
        DateTime updated_at,
        Guid? updated_by);

    private sealed record _UserPermissionRow(
        string module,
        string sub_module,
        bool can_view,
        bool can_insert,
        bool can_edit,
        bool can_delete);
}
