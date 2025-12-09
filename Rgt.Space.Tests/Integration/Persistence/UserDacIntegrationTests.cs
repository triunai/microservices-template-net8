using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly.Registry;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Infrastructure.Persistence.Dac.Identity;
using Rgt.Space.Infrastructure.Resilience;
using Testcontainers.PostgreSql;

namespace Rgt.Space.Tests.Integration.Persistence;

public class UserDacIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container
        _postgres = new PostgreSqlBuilder()
            .WithImage("public.ecr.aws/docker/library/postgres:15-alpine")
            .WithDatabase("test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        await TestDatabaseInitializer.InitializeAsync(_connectionString);
    }

    public async Task DisposeAsync()
    {
        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetPermissionsAsync_ShouldCalculateEffectivePermissionsCorrectly()
    {
        // This test implements the verification logic from TEST_PLAN_RBAC_FIX.md
        
        // Arrange
        var connFactory = new TestSystemConnectionFactory(_connectionString);
        var registry = new ResiliencePipelineRegistry<string>();
        var resilienceSettings = new ResilienceSettings
        {
            MasterDb = new PipelineSettings
            {
                TimeoutMs = 1000,
                RetryCount = 1,
                RetryDelaysMs = new[] { 10 },
                FailureRatio = 0.5,
                SamplingDurationSeconds = 10,
                MinimumThroughput = 2,
                BreakDurationSeconds = 5
            }
        };
        var options = Options.Create(resilienceSettings);
        var logger = Substitute.For<ILogger<UserReadDac>>();
        var dac = new UserReadDac(connFactory, registry, options, logger);

        await SetupRbacTestDataAsync(_connectionString);

        var testUserId = Guid.Parse("01938567-0000-7000-8000-000000000001");

        // Act
        var permissions = await dac.GetPermissionsAsync(testUserId, CancellationToken.None);

        // Assert
        // 1. Projects Module:
        //    can_view: true (Inherited from Role)
        //    can_edit: false (Blocked by Deny Override)
        var projectPerms = permissions.FirstOrDefault(p => p.Module == "PROJECTS" && p.SubModule == "PROJECT_DETAILS");
        projectPerms.Should().NotBeNull("PROJECTS module permissions should exist");
        projectPerms!.CanView.Should().BeTrue("Should be able to view projects (Role grant)");
        projectPerms.CanEdit.Should().BeFalse("Should NOT be able to edit projects (Deny override)");

        // 2. Clients Module:
        //    can_view: true (Granted by Allow Override)
        var clientPerms = permissions.FirstOrDefault(p => p.Module == "CLIENTS" && p.SubModule == "CLIENT_DETAILS");
        clientPerms.Should().NotBeNull("CLIENTS module permissions should exist");
        clientPerms!.CanView.Should().BeTrue("Should be able to view clients (Allow override)");
    }

    private async Task SetupRbacTestDataAsync(string connectionString)
    {
        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Ensure prerequisite data exists (Modules, Resources, Actions, Permissions)
        // We'll rely on seed data if possible, or create minimum viable data.
        // Since we didn't run a seed script, we must insert the metadata.

        var sql = @"
            -- 0. Modules, Resources, Actions (Simplified)
            INSERT INTO modules (id, name, code) VALUES
            (uuid_generate_v7(), 'Projects', 'PROJECTS'),
            (uuid_generate_v7(), 'Clients', 'CLIENTS')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO resources (id, module_id, name, code)
            SELECT uuid_generate_v7(), id, 'Project Details', 'PROJECT_DETAILS' FROM modules WHERE code = 'PROJECTS'
            ON CONFLICT DO NOTHING;

            INSERT INTO resources (id, module_id, name, code)
            SELECT uuid_generate_v7(), id, 'Client Details', 'CLIENT_DETAILS' FROM modules WHERE code = 'CLIENTS'
            ON CONFLICT DO NOTHING;

            INSERT INTO actions (id, name, code) VALUES
            (uuid_generate_v7(), 'View', 'VIEW'),
            (uuid_generate_v7(), 'Edit', 'EDIT')
            ON CONFLICT (code) DO NOTHING;

            -- Permissions
            INSERT INTO permissions (id, resource_id, action_id, code)
            SELECT uuid_generate_v7(), r.id, a.id, 'PROJECTS_VIEW'
            FROM resources r, actions a
            WHERE r.code = 'PROJECT_DETAILS' AND a.code = 'VIEW'
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO permissions (id, resource_id, action_id, code)
            SELECT uuid_generate_v7(), r.id, a.id, 'PROJECTS_EDIT'
            FROM resources r, actions a
            WHERE r.code = 'PROJECT_DETAILS' AND a.code = 'EDIT'
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO permissions (id, resource_id, action_id, code)
            SELECT uuid_generate_v7(), r.id, a.id, 'CLIENTS_VIEW'
            FROM resources r, actions a
            WHERE r.code = 'CLIENT_DETAILS' AND a.code = 'VIEW'
            ON CONFLICT (code) DO NOTHING;


            -- 1. Create a Test User
            INSERT INTO users (id, display_name, email, is_active)
            VALUES ('01938567-0000-7000-8000-000000000001', 'Test User', 'test.user@example.com', TRUE)
            ON CONFLICT (email) DO NOTHING;

            -- 2. Create a 'Project Manager' Role
            INSERT INTO roles (id, name, code, is_active)
            VALUES ('01938567-0000-7000-8000-000000000002', 'Project Manager', 'PROJECT_MANAGER', TRUE)
            ON CONFLICT (code) DO NOTHING;

            -- 3. Grant 'PROJECT_VIEW' and 'PROJECT_EDIT' to the Role
            INSERT INTO role_permissions (role_id, permission_id)
            SELECT '01938567-0000-7000-8000-000000000002', p.id
            FROM permissions p
            WHERE p.code IN ('PROJECTS_VIEW', 'PROJECTS_EDIT')
            ON CONFLICT DO NOTHING;

            -- 4. Assign Role to User
            INSERT INTO user_roles (user_id, role_id)
            VALUES ('01938567-0000-7000-8000-000000000001', '01938567-0000-7000-8000-000000000002')
            ON CONFLICT DO NOTHING;

            -- 5. Add a DENY Override for 'PROJECTS_EDIT' (The 'Exception')
            INSERT INTO user_permission_overrides (user_id, permission_id, is_allowed)
            SELECT '01938567-0000-7000-8000-000000000001', p.id, FALSE
            FROM permissions p
            WHERE p.code = 'PROJECTS_EDIT'
            ON CONFLICT DO NOTHING;

            -- 6. Add an ALLOW Override for 'CLIENTS_VIEW' (The 'Bonus')
            INSERT INTO user_permission_overrides (user_id, permission_id, is_allowed)
            SELECT '01938567-0000-7000-8000-000000000001', p.id, TRUE
            FROM permissions p
            WHERE p.code = 'CLIENTS_VIEW'
            ON CONFLICT DO NOTHING;
        ";

        await conn.ExecuteAsync(sql);
    }
}
