using Dapper;
using Npgsql;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Infrastructure.Persistence.Identity;

public sealed class UserReadDac : IUserReadDac
{
    private readonly ITenantConnectionFactory _connFactory;

    public UserReadDac(ITenantConnectionFactory connFactory)
    {
        _connFactory = connFactory;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetSqlConnectionStringAsync(string.Empty, ct);
        await using var conn = new NpgsqlConnection(connString);

        var dto = await conn.QuerySingleOrDefaultAsync<UserDto>(
            @"SELECT * FROM users WHERE id = @Id AND is_deleted = false",
            new { Id = id });

        return dto?.ToEntity();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetSqlConnectionStringAsync(string.Empty, ct);
        await using var conn = new NpgsqlConnection(connString);

        var dto = await conn.QuerySingleOrDefaultAsync<UserDto>(
            @"SELECT * FROM users WHERE email = @Email AND is_deleted = false",
            new { Email = email });

        return dto?.ToEntity();
    }

    public async Task<User?> GetByExternalIdAsync(string provider, string externalId, CancellationToken ct = default)
    {
        var connString = await _connFactory.GetSqlConnectionStringAsync(string.Empty, ct);
        await using var conn = new NpgsqlConnection(connString);

        var dto = await conn.QuerySingleOrDefaultAsync<UserDto>(
            @"SELECT * FROM users WHERE sso_provider = @Provider AND external_id = @ExternalId AND is_deleted = false",
            new { Provider = provider, ExternalId = externalId });

        return dto?.ToEntity();
    }

    // DTO to match DB schema (snake_case columns mapped by Dapper DefaultTypeMap or manually)
    // We rely on Dapper's DefaultTypeMap.MatchNamesWithUnderscores = true; being set globally usually.
    // If not, we need [Column] attributes or manual mapping.
    // Assuming standard Dapper setup. If not, I'll use explicit aliases in SQL.
    // Let's use explicit aliases to be safe and robust.
    private sealed class UserDto
    {
        public Guid id { get; set; }
        public string display_name { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string? contact_number { get; set; }
        public bool is_active { get; set; }
        public bool local_login_enabled { get; set; }
        public byte[]? password_hash { get; set; }
        public byte[]? password_salt { get; set; }
        public bool sso_login_enabled { get; set; }
        public string? sso_provider { get; set; }
        public string? external_id { get; set; }
        public string? sso_email { get; set; }
        public DateTime? last_login_at { get; set; }
        public string? last_login_provider { get; set; }
        public DateTime created_at { get; set; }
        public Guid? created_by { get; set; }
        public DateTime updated_at { get; set; }
        public Guid? updated_by { get; set; }

        public User ToEntity() => User.Rehydrate(
            id, display_name, email, contact_number, is_active,
            local_login_enabled, password_hash, password_salt,
            sso_login_enabled, sso_provider, external_id, sso_email,
            last_login_at, last_login_provider,
            created_at, created_by, updated_at, updated_by);
    }
}
