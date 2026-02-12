using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Persistence.Dac.Features;

public sealed class FeatureReadDac : IFeatureReadDac
{
    private readonly ISystemConnectionFactory _connFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<FeatureReadDac> _logger;

    public FeatureReadDac(
        ISystemConnectionFactory connFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<FeatureReadDac> logger)
    {
        _connFactory = connFactory;
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task<FeatureReadModel?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            // Explicit OpenAsync(token) is REQUIRED so the CancellationToken propagates to TCP connect.
            // Without it, Dapper auto-opens without a token and a DB outage hangs for ~30s (TCP timeout)
            // instead of failing fast at the Polly timeout (4s). FeatureWriteDac already does this correctly.
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT id, code, name, description, is_active, requires_client,
                       created_at, created_by, updated_at, updated_by
                FROM features
                WHERE code = @Code AND is_deleted = FALSE";

            var cmd = new CommandDefinition(sql, new { Code = code },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var row = await conn.QuerySingleOrDefaultAsync<_FeatureRow>(cmd);
            return row?.ToReadModel();
        }, ct);
    }

    public async Task<FeatureReadModel?> GetByIdAsync(Guid featureId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            // Explicit OpenAsync(token) is REQUIRED so the CancellationToken propagates to TCP connect.
            // Without it, Dapper auto-opens without a token and a DB outage hangs for ~30s (TCP timeout)
            // instead of failing fast at the Polly timeout (4s). FeatureWriteDac already does this correctly.
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT id, code, name, description, is_active, requires_client,
                       created_at, created_by, updated_at, updated_by
                FROM features
                WHERE id = @FeatureId AND is_deleted = FALSE";

            var cmd = new CommandDefinition(sql, new { FeatureId = featureId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var row = await conn.QuerySingleOrDefaultAsync<_FeatureRow>(cmd);
            return row?.ToReadModel();
        }, ct);
    }

    public async Task<ClientFeatureReadModel?> GetClientFeatureAsync(Guid clientId, Guid featureId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            // Explicit OpenAsync(token) is REQUIRED so the CancellationToken propagates to TCP connect.
            // Without it, Dapper auto-opens without a token and a DB outage hangs for ~30s (TCP timeout)
            // instead of failing fast at the Polly timeout (4s). FeatureWriteDac already does this correctly.
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT id, client_id, feature_id, is_enabled,
                       created_at, created_by, updated_at, updated_by
                FROM client_features
                WHERE client_id = @ClientId AND feature_id = @FeatureId AND is_deleted = FALSE";

            var cmd = new CommandDefinition(sql, new { ClientId = clientId, FeatureId = featureId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var row = await conn.QuerySingleOrDefaultAsync<_ClientFeatureRow>(cmd);
            return row?.ToReadModel();
        }, ct);
    }

    public async Task<UserOverrideReadModel?> GetUserOverrideAsync(Guid userId, Guid featureId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            // Explicit OpenAsync(token) is REQUIRED so the CancellationToken propagates to TCP connect.
            // Without it, Dapper auto-opens without a token and a DB outage hangs for ~30s (TCP timeout)
            // instead of failing fast at the Polly timeout (4s). FeatureWriteDac already does this correctly.
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT id, user_id, feature_id, override_state, reason,
                       created_at, created_by
                FROM user_feature_overrides
                WHERE user_id = @UserId AND feature_id = @FeatureId";

            var cmd = new CommandDefinition(sql, new { UserId = userId, FeatureId = featureId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var row = await conn.QuerySingleOrDefaultAsync<_UserOverrideRow>(cmd);
            return row?.ToReadModel();
        }, ct);
    }

    public async Task<IReadOnlyList<FeatureReadModel>> GetAllAsync(CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            // Explicit OpenAsync(token) is REQUIRED so the CancellationToken propagates to TCP connect.
            // Without it, Dapper auto-opens without a token and a DB outage hangs for ~30s (TCP timeout)
            // instead of failing fast at the Polly timeout (4s). FeatureWriteDac already does this correctly.
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT id, code, name, description, is_active, requires_client,
                       created_at, created_by, updated_at, updated_by
                FROM features
                WHERE is_deleted = FALSE
                ORDER BY code ASC
                LIMIT 1000";

            var cmd = new CommandDefinition(sql,
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var rows = await conn.QueryAsync<_FeatureRow>(cmd);
            return rows.Select(r => r.ToReadModel()).ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<ClientFeatureDetailReadModel>> GetClientSubscriptionsByFeatureAsync(Guid featureId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT cf.id, cf.client_id, c.name AS client_name, c.code AS client_code,
                       cf.feature_id, cf.is_enabled, cf.created_at, cf.updated_at
                FROM client_features cf
                INNER JOIN clients c ON c.id = cf.client_id AND c.is_deleted = FALSE
                WHERE cf.feature_id = @FeatureId AND cf.is_deleted = FALSE
                ORDER BY c.name ASC
                LIMIT 1000";

            var cmd = new CommandDefinition(sql, new { FeatureId = featureId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var rows = await conn.QueryAsync<_ClientFeatureDetailRow>(cmd);
            return rows.Select(r => r.ToReadModel()).ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<ClientFeatureByClientReadModel>> GetFeaturesByClientAsync(Guid clientId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT cf.id, cf.client_id, cf.feature_id,
                       f.code AS feature_code, f.name AS feature_name,
                       cf.is_enabled, f.is_active AS feature_is_active,
                       cf.created_at, cf.updated_at
                FROM client_features cf
                INNER JOIN features f ON f.id = cf.feature_id AND f.is_deleted = FALSE
                WHERE cf.client_id = @ClientId AND cf.is_deleted = FALSE
                ORDER BY f.code ASC
                LIMIT 1000";

            var cmd = new CommandDefinition(sql, new { ClientId = clientId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var rows = await conn.QueryAsync<_ClientFeatureByClientRow>(cmd);
            return rows.Select(r => r.ToReadModel()).ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<UserOverrideDetailReadModel>> GetUserOverridesByFeatureAsync(Guid featureId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT ufo.id, ufo.user_id, u.display_name AS user_display_name, u.email AS user_email,
                       ufo.feature_id, ufo.override_state, ufo.reason,
                       ufo.created_at, ufo.created_by
                FROM user_feature_overrides ufo
                INNER JOIN users u ON u.id = ufo.user_id AND u.is_active = TRUE
                WHERE ufo.feature_id = @FeatureId
                ORDER BY u.display_name ASC
                LIMIT 1000";

            var cmd = new CommandDefinition(sql, new { FeatureId = featureId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var rows = await conn.QueryAsync<_UserOverrideDetailRow>(cmd);
            return rows.Select(r => r.ToReadModel()).ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<UserOverrideByUserReadModel>> GetOverridesByUserAsync(Guid userId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT ufo.id, ufo.user_id, ufo.feature_id,
                       f.code AS feature_code, f.name AS feature_name,
                       ufo.override_state, ufo.reason,
                       ufo.created_at, ufo.created_by
                FROM user_feature_overrides ufo
                INNER JOIN features f ON f.id = ufo.feature_id AND f.is_deleted = FALSE
                WHERE ufo.user_id = @UserId
                ORDER BY f.code ASC
                LIMIT 1000";

            var cmd = new CommandDefinition(sql, new { UserId = userId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var rows = await conn.QueryAsync<_UserOverrideByUserRow>(cmd);
            return rows.Select(r => r.ToReadModel()).ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<ClientFeatureReadModel>> GetClientFeaturesByClientAsync(Guid clientId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT id, client_id, feature_id, is_enabled,
                       created_at, created_by, updated_at, updated_by
                FROM client_features
                WHERE client_id = @ClientId AND is_deleted = FALSE
                LIMIT 1000";

            var cmd = new CommandDefinition(sql, new { ClientId = clientId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var rows = await conn.QueryAsync<_ClientFeatureRow>(cmd);
            return rows.Select(r => r.ToReadModel()).ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<UserOverrideReadModel>> GetUserOverridesByUserAsync(Guid userId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);
            const string sql = @"
                SELECT id, user_id, feature_id, override_state, reason,
                       created_at, created_by
                FROM user_feature_overrides
                WHERE user_id = @UserId
                LIMIT 1000";

            var cmd = new CommandDefinition(sql, new { UserId = userId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var rows = await conn.QueryAsync<_UserOverrideRow>(cmd);
            return rows.Select(r => r.ToReadModel()).ToList();
        }, ct);
    }

    // Private Dapper row types — sealed records per DAC convention
    private sealed record _FeatureRow(
        Guid id, string code, string name, string? description,
        bool is_active, bool requires_client,
        DateTime created_at, Guid? created_by, DateTime updated_at, Guid? updated_by)
    {
        public FeatureReadModel ToReadModel() => new(
            id, code, name, description, is_active, requires_client,
            created_at, created_by, updated_at, updated_by);
    }

    private sealed record _ClientFeatureRow(
        Guid id, Guid client_id, Guid feature_id, bool is_enabled,
        DateTime created_at, Guid? created_by, DateTime updated_at, Guid? updated_by)
    {
        public ClientFeatureReadModel ToReadModel() => new(
            id, client_id, feature_id, is_enabled,
            created_at, created_by, updated_at, updated_by);
    }

    private sealed record _UserOverrideRow(
        Guid id, Guid user_id, Guid feature_id, string override_state, string? reason,
        DateTime created_at, Guid? created_by)
    {
        public UserOverrideReadModel ToReadModel() => new(
            id, user_id, feature_id, override_state, reason,
            created_at, created_by);
    }

    private sealed record _ClientFeatureDetailRow(
        Guid id, Guid client_id, string client_name, string client_code,
        Guid feature_id, bool is_enabled, DateTime created_at, DateTime updated_at)
    {
        public ClientFeatureDetailReadModel ToReadModel() => new(
            id, client_id, client_name, client_code,
            feature_id, is_enabled, created_at, updated_at);
    }

    private sealed record _ClientFeatureByClientRow(
        Guid id, Guid client_id, Guid feature_id,
        string feature_code, string feature_name,
        bool is_enabled, bool feature_is_active,
        DateTime created_at, DateTime updated_at)
    {
        public ClientFeatureByClientReadModel ToReadModel() => new(
            id, client_id, feature_id,
            feature_code, feature_name,
            is_enabled, feature_is_active,
            created_at, updated_at);
    }

    private sealed record _UserOverrideDetailRow(
        Guid id, Guid user_id, string user_display_name, string user_email,
        Guid feature_id, string override_state, string? reason,
        DateTime created_at, Guid? created_by)
    {
        public UserOverrideDetailReadModel ToReadModel() => new(
            id, user_id, user_display_name, user_email,
            feature_id, override_state, reason,
            created_at, created_by);
    }

    private sealed record _UserOverrideByUserRow(
        Guid id, Guid user_id, Guid feature_id,
        string feature_code, string feature_name,
        string override_state, string? reason,
        DateTime created_at, Guid? created_by)
    {
        public UserOverrideByUserReadModel ToReadModel() => new(
            id, user_id, feature_id,
            feature_code, feature_name,
            override_state, reason,
            created_at, created_by);
    }
}
