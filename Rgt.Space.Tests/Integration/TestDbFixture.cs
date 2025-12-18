using Testcontainers.PostgreSql;

namespace Rgt.Space.Tests.Integration;

public class TestDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    public string ConnectionString { get; private set; } = string.Empty;

    public TestDbFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("public.ecr.aws/docker/library/postgres:15-alpine")
            .WithDatabase("test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        // Initialize schema
        await using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // We might want to read schema from files, but for now let's just create what we need or empty.
        // Actually, if we use WebApplicationFactory, it might run migrations?
        // The project seems to use Dapper so maybe no automatic migrations.
        // I will copy the schema setup from UserDacIntegrationTests or leave it for now and let tests handle it or helper.
        // For E2E tests, the app usually expects tables to exist.

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            -- Create UUID v7 function
            CREATE OR REPLACE FUNCTION uuid_generate_v7()
            RETURNS uuid
            AS $$
            DECLARE
              unix_ts_ms bytea;
              uuid_bytes bytea;
            BEGIN
              unix_ts_ms = substring(int8send(floor(extract(epoch from clock_timestamp()) * 1000)::bigint) from 3);
              uuid_bytes = unix_ts_ms || gen_random_bytes(10);
              uuid_bytes = set_byte(uuid_bytes, 6, (get_byte(uuid_bytes, 6) & x'0f'::int) | x'70'::int);
              uuid_bytes = set_byte(uuid_bytes, 8, (get_byte(uuid_bytes, 8) & x'3f'::int) | x'80'::int);
              RETURN encode(uuid_bytes, 'hex')::uuid;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
                display_name TEXT NOT NULL,
                email TEXT NOT NULL,
                contact_number TEXT NULL,
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                local_login_enabled BOOLEAN NOT NULL DEFAULT TRUE,
                password_hash BYTEA NULL,
                password_salt BYTEA NULL,
                sso_login_enabled BOOLEAN NOT NULL DEFAULT FALSE,
                sso_provider TEXT NULL,
                external_id TEXT NULL,
                sso_email TEXT NULL,
                last_login_at TIMESTAMP NULL,
                last_login_provider TEXT NULL,
                created_at TIMESTAMP NOT NULL DEFAULT now(),
                created_by UUID NULL,
                updated_at TIMESTAMP NOT NULL DEFAULT now(),
                updated_by UUID NULL,
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                deleted_at TIMESTAMP NULL,
                deleted_by UUID NULL,
                CONSTRAINT users_email_uk UNIQUE (email),
                CONSTRAINT users_sso_uk UNIQUE (sso_provider, external_id)
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "TRUNCATE TABLE users;";
        await cmd.ExecuteNonQueryAsync();
    }
}
