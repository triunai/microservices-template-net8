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
    private readonly ITenantConnectionFactory _connFactory;
    private readonly ITenantProvider _tenant;
    private readonly ResiliencePipelineRegistry<string> _pipelineRegistry;
    private readonly IOptions<ResilienceSettings> _resilienceSettings;
    private readonly ILogger<UserReadDac> _logger;

    public UserReadDac(
        ITenantConnectionFactory connFactory,
        ITenantProvider tenant,
        ResiliencePipelineRegistry<string> pipelineRegistry,
        IOptions<ResilienceSettings> resilienceSettings,
        ILogger<UserReadDac> logger)
    {
        _connFactory = connFactory;
        _tenant = tenant;
        _pipelineRegistry = pipelineRegistry;
        _resilienceSettings = resilienceSettings;
        _logger = logger;
    }

    public async Task<UserReadModel?> GetByIdAsync(Guid userId, CancellationToken ct)
    {
        var tenantId = _tenant.Id!;
        var pipeline = GetOrCreatePipeline(tenantId);
        
        _logger.LogDebug("Querying user {UserId} for tenant {TenantId}", userId, tenantId);
        
        var connectionString = await _connFactory.GetSqlConnectionStringAsync(tenantId, ct);
        
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
                    updated_at
                FROM users
                WHERE id = @UserId AND is_deleted = FALSE";

            var result = await conn.QuerySingleOrDefaultAsync<_UserRow>(
                sql,
                new { UserId = userId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

            if (result is null)
            {
                _logger.LogDebug("User {UserId} not found for tenant {TenantId}", userId, tenantId);
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
                result.updated_at);
        }, ct);
    }

    public async Task<UserReadModel?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var tenantId = _tenant.Id!;
        var pipeline = GetOrCreatePipeline(tenantId);
        
        var connectionString = await _connFactory.GetSqlConnectionStringAsync(tenantId, ct);
        
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
                    updated_at
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
                result.updated_at);
        }, ct);
    }

    public async Task<UserReadModel?> GetByExternalIdAsync(string provider, string externalId, CancellationToken ct)
    {
        var tenantId = _tenant.Id!;
        var pipeline = GetOrCreatePipeline(tenantId);
        
        var connectionString = await _connFactory.GetSqlConnectionStringAsync(tenantId, ct);
        
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
                    updated_at
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
                result.updated_at);
        }, ct);
    }

    public async Task<IReadOnlyList<UserReadModel>> GetAllAsync(CancellationToken ct)
    {
        var tenantId = _tenant.Id!;
        var pipeline = GetOrCreatePipeline(tenantId);
        
        var connectionString = await _connFactory.GetSqlConnectionStringAsync(tenantId, ct);
        
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
                    updated_at
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
                    r.updated_at))
                .ToList();
        }, ct);
    }

    private ResiliencePipeline GetOrCreatePipeline(string tenantId)
    {
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
        DateTime updated_at);
}
