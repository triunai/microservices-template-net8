using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Infrastructure.Persistence.Dac.Features;

public sealed class FeatureWriteDac : IFeatureWriteDac
{
    private readonly ISystemConnectionFactory _connFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<FeatureWriteDac> _logger;

    public FeatureWriteDac(
        ISystemConnectionFactory connFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<FeatureWriteDac> logger)
    {
        _connFactory = connFactory;
        _pipeline = pipelineProvider.GetPipeline("PortalDb");
        _logger = logger;
    }

    public async Task<Guid> CreateFeatureAsync(Guid id, string code, string name, string? description,
        bool isActive, bool requiresClient, Guid createdBy, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);

            const string sql = @"
                INSERT INTO features (id, code, name, description, is_active, requires_client, created_by, updated_by)
                VALUES (@Id, @Code, @Name, @Description, @IsActive, @RequiresClient, @CreatedBy, @CreatedBy)
                RETURNING id";

            try
            {
                var p = new
                {
                    Id = id,
                    Code = code,
                    Name = name,
                    Description = description,
                    IsActive = isActive,
                    RequiresClient = requiresClient,
                    CreatedBy = createdBy
                };
                var cmd = new CommandDefinition(sql, p,
                    commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
                return await conn.ExecuteScalarAsync<Guid>(cmd);
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                if (ex.ConstraintName?.Contains("code") == true)
                {
                    throw new ConflictException(ErrorCatalog.FEATURE_CODE_EXISTS, $"Feature code '{code}' already exists.");
                }
                throw;
            }
        }, ct);
    }

    // Returns bool instead of Task because the WHERE clause filters is_deleted = FALSE.
    // A concurrent soft-delete between the handler's existence check and this write
    // causes 0 rows affected — returning bool lets the handler detect this TOCTOU race
    // and return FEATURE_NOT_FOUND instead of a misleading 204 No Content.
    //
    // Cascades soft-delete to client_features and hard-deletes user_feature_overrides.
    // Without cascade, soft-deleting a feature leaves orphaned active client_features rows
    // and user_feature_overrides rows pointing to a deleted feature. If the same feature code
    // is re-created later, it gets a new UUID — the old orphaned rows never match.
    // user_feature_overrides use hard delete (matching their existing hard-delete pattern).
    public async Task<bool> SoftDeleteFeatureAsync(Guid featureId, Guid deletedBy, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);

            // CTE chains the cascade: soft-delete feature → soft-delete client subs → hard-delete user overrides.
            // The final SELECT COUNT(*) returns 1 if the feature existed, 0 if already deleted (TOCTOU check).
            // We can't use ExecuteAsync here because its return value is the last DML's row count,
            // which would be the user_feature_overrides DELETE count — not the feature delete count.
            const string sql = @"
                WITH deleted_feature AS (
                    UPDATE features
                    SET is_deleted = TRUE,
                        deleted_by = @DeletedBy,
                        deleted_at = NOW() AT TIME ZONE 'utc'
                    WHERE id = @FeatureId AND is_deleted = FALSE
                    RETURNING id
                ),
                cascade_client_features AS (
                    UPDATE client_features
                    SET is_deleted = TRUE,
                        deleted_by = @DeletedBy,
                        deleted_at = NOW() AT TIME ZONE 'utc'
                    WHERE feature_id IN (SELECT id FROM deleted_feature) AND is_deleted = FALSE
                ),
                cascade_user_overrides AS (
                    DELETE FROM user_feature_overrides
                    WHERE feature_id IN (SELECT id FROM deleted_feature)
                )
                SELECT COUNT(*) FROM deleted_feature";

            var cmd = new CommandDefinition(sql, new { FeatureId = featureId, DeletedBy = deletedBy },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var deletedCount = await conn.ExecuteScalarAsync<int>(cmd);
            return deletedCount > 0;
        }, ct);
    }

    // Same TOCTOU rationale as SoftDeleteFeatureAsync above.
    public async Task<bool> UpdateFeatureAsync(Guid featureId, string name, string? description,
        bool isActive, bool requiresClient, Guid updatedBy, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        return await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);

            const string sql = @"
                UPDATE features
                SET name = @Name,
                    description = @Description,
                    is_active = @IsActive,
                    requires_client = @RequiresClient,
                    updated_by = @UpdatedBy,
                    updated_at = NOW() AT TIME ZONE 'utc'
                WHERE id = @FeatureId AND is_deleted = FALSE";

            var cmd = new CommandDefinition(sql,
                new { FeatureId = featureId, Name = name, Description = description,
                      IsActive = isActive, RequiresClient = requiresClient, UpdatedBy = updatedBy },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            var rowsAffected = await conn.ExecuteAsync(cmd);
            return rowsAffected > 0;
        }, ct);
    }

    public async Task UpsertClientFeatureAsync(Guid clientId, Guid featureId, bool isEnabled, Guid updatedBy, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);

            var newId = Uuid7.NewUuid7();

            const string sql = @"
                INSERT INTO client_features (id, client_id, feature_id, is_enabled, created_by, updated_by)
                VALUES (@Id, @ClientId, @FeatureId, @IsEnabled, @UpdatedBy, @UpdatedBy)
                ON CONFLICT (client_id, feature_id) WHERE is_deleted = FALSE
                DO UPDATE SET is_enabled = @IsEnabled,
                              updated_by = @UpdatedBy,
                              updated_at = NOW() AT TIME ZONE 'utc'";

            var cmd = new CommandDefinition(sql,
                new { Id = newId, ClientId = clientId, FeatureId = featureId, IsEnabled = isEnabled, UpdatedBy = updatedBy },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            await conn.ExecuteAsync(cmd);
        }, ct);
    }

    public async Task SoftDeleteClientFeatureAsync(Guid clientId, Guid featureId, Guid deletedBy, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);

            const string sql = @"
                UPDATE client_features
                SET is_deleted = TRUE,
                    deleted_by = @DeletedBy,
                    deleted_at = NOW() AT TIME ZONE 'utc'
                WHERE client_id = @ClientId AND feature_id = @FeatureId AND is_deleted = FALSE";

            var cmd = new CommandDefinition(sql,
                new { ClientId = clientId, FeatureId = featureId, DeletedBy = deletedBy },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            await conn.ExecuteAsync(cmd);
        }, ct);
    }

    public async Task SetUserOverrideAsync(Guid userId, Guid featureId, string overrideState,
        string? reason, Guid createdBy, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);

            var newId = Uuid7.NewUuid7();

            const string sql = @"
                INSERT INTO user_feature_overrides (id, user_id, feature_id, override_state, reason, created_by)
                VALUES (@Id, @UserId, @FeatureId, @OverrideState, @Reason, @CreatedBy)
                ON CONFLICT (user_id, feature_id)
                DO UPDATE SET override_state = @OverrideState,
                              reason = @Reason,
                              created_by = @CreatedBy,
                              created_at = NOW() AT TIME ZONE 'utc'";

            var cmd = new CommandDefinition(sql,
                new { Id = newId, UserId = userId, FeatureId = featureId, OverrideState = overrideState, Reason = reason, CreatedBy = createdBy },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            await conn.ExecuteAsync(cmd);
        }, ct);
    }

    public async Task ClearUserOverrideAsync(Guid userId, Guid featureId, CancellationToken ct)
    {
        var connString = await _connFactory.GetConnectionStringAsync(ct);
        await _pipeline.ExecuteAsync(async token =>
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(token);

            const string sql = @"
                DELETE FROM user_feature_overrides
                WHERE user_id = @UserId AND feature_id = @FeatureId";

            var cmd = new CommandDefinition(sql, new { UserId = userId, FeatureId = featureId },
                commandTimeout: SqlConstants.CommandTimeouts.TenantDb, cancellationToken: token);
            await conn.ExecuteAsync(cmd);
        }, ct);
    }
}
