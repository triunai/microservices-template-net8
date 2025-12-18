using Testcontainers.PostgreSql;
using Npgsql;
using Dapper;

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

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        // Full Schema Setup
        cmd.CommandText = @"
            -- UUID v7
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

            -- 1. Users
            CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
                display_name TEXT NOT NULL,
                email TEXT NOT NULL,
                contact_number TEXT NULL,
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                local_login_enabled BOOLEAN NOT NULL DEFAULT TRUE,
                password_hash BYTEA NULL,
                password_salt BYTEA NULL,
                password_expiry_at      TIMESTAMP   NULL,
                password_reset_token    TEXT        NULL,
                password_reset_expires_at TIMESTAMP NULL,
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

            -- 2. Roles
            CREATE TABLE IF NOT EXISTS roles (
                id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
                name        TEXT        NOT NULL,
                code        TEXT        NOT NULL,
                description TEXT        NULL,
                is_system   BOOLEAN     NOT NULL DEFAULT FALSE,
                is_active   BOOLEAN     NOT NULL DEFAULT TRUE,
                created_at  TIMESTAMP   NOT NULL DEFAULT now(),
                created_by  UUID        NULL REFERENCES users (id),
                updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
                updated_by  UUID        NULL REFERENCES users (id),
                CONSTRAINT roles_code_uk UNIQUE (code)
            );

            -- 3. Modules
            CREATE TABLE IF NOT EXISTS modules (
                id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
                name       TEXT        NOT NULL,
                code       TEXT        NOT NULL,
                is_active  BOOLEAN     NOT NULL DEFAULT TRUE,
                sort_order INT         NULL,
                created_at TIMESTAMP   NOT NULL DEFAULT now(),
                created_by UUID        NULL REFERENCES users (id),
                updated_at TIMESTAMP   NOT NULL DEFAULT now(),
                updated_by UUID        NULL REFERENCES users (id),
                is_deleted BOOLEAN     NOT NULL DEFAULT FALSE,
                deleted_at TIMESTAMP   NULL,
                deleted_by UUID        NULL REFERENCES users (id),
                CONSTRAINT modules_code_uk UNIQUE (code)
            );

            -- 4. Resources
            CREATE TABLE IF NOT EXISTS resources (
                id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
                module_id  UUID        NOT NULL REFERENCES modules (id),
                name       TEXT        NOT NULL,
                code       TEXT        NOT NULL,
                created_at TIMESTAMP   NOT NULL DEFAULT now(),
                created_by UUID        NULL REFERENCES users (id),
                updated_at TIMESTAMP   NOT NULL DEFAULT now(),
                updated_by UUID        NULL REFERENCES users (id),
                is_deleted BOOLEAN     NOT NULL DEFAULT FALSE,
                deleted_at TIMESTAMP   NULL,
                deleted_by UUID        NULL REFERENCES users (id),
                CONSTRAINT resources_module_code_uk UNIQUE (module_id, code)
            );

            -- 5. Actions
            CREATE TABLE IF NOT EXISTS actions (
                id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
                name       TEXT        NOT NULL,
                code       TEXT        NOT NULL,
                created_at TIMESTAMP   NOT NULL DEFAULT now(),
                created_by UUID        NULL REFERENCES users (id),
                updated_at TIMESTAMP   NOT NULL DEFAULT now(),
                updated_by UUID        NULL REFERENCES users (id),
                CONSTRAINT actions_code_uk UNIQUE (code)
            );

            -- 6. Permissions
            CREATE TABLE IF NOT EXISTS permissions (
                id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
                resource_id UUID        NOT NULL REFERENCES resources (id),
                action_id   UUID        NOT NULL REFERENCES actions (id),
                code        TEXT        NOT NULL,
                description TEXT        NULL,
                created_at  TIMESTAMP   NOT NULL DEFAULT now(),
                created_by  UUID        NULL REFERENCES users (id),
                updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
                updated_by  UUID        NULL REFERENCES users (id),
                CONSTRAINT permissions_resource_action_uk UNIQUE (resource_id, action_id),
                CONSTRAINT permissions_code_uk            UNIQUE (code)
            );

            -- 7. Role Permissions
            CREATE TABLE IF NOT EXISTS role_permissions (
                role_id       UUID NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
                permission_id UUID NOT NULL REFERENCES permissions (id) ON DELETE CASCADE,
                CONSTRAINT role_permissions_pk PRIMARY KEY (role_id, permission_id)
            );

            -- 8. User Roles
            CREATE TABLE IF NOT EXISTS user_roles (
                id                  UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
                user_id             UUID        NOT NULL REFERENCES users   (id),
                role_id             UUID        NOT NULL REFERENCES roles   (id),
                assigned_by_user_id UUID        NULL REFERENCES users (id),
                assigned_at         TIMESTAMP   NOT NULL DEFAULT now(),
                CONSTRAINT user_roles_uk UNIQUE (user_id, role_id)
            );

            -- 9. User Permission Overrides
            CREATE TABLE IF NOT EXISTS user_permission_overrides (
                id            UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
                user_id       UUID        NOT NULL REFERENCES users   (id),
                permission_id UUID        NOT NULL REFERENCES permissions (id),
                is_allowed    BOOLEAN     NOT NULL,
                reason        TEXT        NULL,
                created_at    TIMESTAMP   NOT NULL DEFAULT now(),
                created_by    UUID        NULL REFERENCES users (id),
                CONSTRAINT user_permission_overrides_uk UNIQUE (user_id, permission_id)
            );

            -- 10. Clients
            CREATE TABLE IF NOT EXISTS clients (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
                code VARCHAR(50) NOT NULL,
                name VARCHAR(255) NOT NULL,
                status VARCHAR(20) NOT NULL DEFAULT 'Active',
                created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
                created_by UUID NULL REFERENCES users(id),
                updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
                updated_by UUID NULL REFERENCES users(id),
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
                deleted_by UUID NULL REFERENCES users(id)
            );

            -- 11. Projects
            CREATE TABLE IF NOT EXISTS projects (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
                code VARCHAR(50) NOT NULL,
                name VARCHAR(255) NOT NULL,
                client_id UUID NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
                external_url TEXT NULL,
                status VARCHAR(20) NOT NULL DEFAULT 'Active',
                created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
                created_by UUID NULL REFERENCES users(id),
                updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
                updated_by UUID NULL REFERENCES users(id),
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
                deleted_by UUID NULL REFERENCES users(id)
            );

            -- 12. Position Types
            CREATE TABLE IF NOT EXISTS position_types (
                code VARCHAR(20) PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                description TEXT NULL,
                sort_order INT NOT NULL UNIQUE,
                status VARCHAR(20) NOT NULL DEFAULT 'Active',
                created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
                updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
            );

            -- 13. Project Assignments
            CREATE TABLE IF NOT EXISTS project_assignments (
                id               UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
                project_id       UUID        NOT NULL REFERENCES projects (id),
                user_id          UUID        NOT NULL REFERENCES users (id),
                position_code    VARCHAR(20) NOT NULL REFERENCES position_types(code),
                assigned_at      TIMESTAMP   NOT NULL DEFAULT now(),
                assigned_by      UUID        NULL REFERENCES users (id),
                created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
                created_by UUID NULL REFERENCES users(id),
                updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
                updated_by UUID NULL REFERENCES users(id),
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
                deleted_by UUID NULL REFERENCES users(id)
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
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Truncate all tables in reverse order of dependencies
        // CASCADE is easier but explicit truncate is safer if we want to keep some seed data (none for now)
        await conn.ExecuteAsync(@"
            TRUNCATE TABLE
                project_assignments,
                projects,
                clients,
                user_permission_overrides,
                user_roles,
                role_permissions,
                permissions,
                actions,
                resources,
                modules,
                roles,
                users,
                position_types
            RESTART IDENTITY CASCADE;");
    }
}
