using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly.Registry;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Core.Domain.Entities.Identity;
using Rgt.Space.Infrastructure.Persistence.Dac.Identity;
using Rgt.Space.Infrastructure.Resilience;
using Rgt.Space.Tests.Integration.Fixtures;

namespace Rgt.Space.Tests.Integration.Persistence;

[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class UserDacIntegrationTests
{
    private readonly TestDbFixture _fixture;
    private string ConnectionString => _fixture.ConnectionString;

    public UserDacIntegrationTests(TestDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetPermissionsAsync_ShouldCalculateEffectivePermissionsCorrectly()
    {
        // This test implements the verification logic from TEST_PLAN_RBAC_FIX.md

        // Arrange
        var connFactory = new TestSystemConnectionFactory(ConnectionString);
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

        await SetupRbacTestDataAsync(ConnectionString);

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

    private IOptions<ResilienceSettings> CreateValidResilienceOptions()
    {
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
        return Options.Create(resilienceSettings);
    }

    [Fact]
    public async Task CreateAsync_ShouldPopulateAuditColumns()
    {
        // Arrange
        var connFactory = new TestSystemConnectionFactory(ConnectionString);
        var writeDac = new UserWriteDac(connFactory);
        var readDac = new UserReadDac(connFactory, new ResiliencePipelineRegistry<string>(), CreateValidResilienceOptions(), Substitute.For<ILogger<UserReadDac>>());

        var user = User.CreateFromSso("audit_test_ext", "audit@example.com", "Audit User", "google");

        // Act
        var userId = await writeDac.CreateAsync(user, CancellationToken.None);
        var persisted = await readDac.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldSetDeletedFlag_And_NotReturnInGetById()
    {
        // Arrange
        var connFactory = new TestSystemConnectionFactory(ConnectionString);
        var writeDac = new UserWriteDac(connFactory);
        var readDac = new UserReadDac(connFactory, new ResiliencePipelineRegistry<string>(), CreateValidResilienceOptions(), Substitute.For<ILogger<UserReadDac>>());

        var user = User.CreateFromSso("delete_test_ext", "delete@example.com", "Delete User", "google");
        var userId = await writeDac.CreateAsync(user, CancellationToken.None);

        // Act
        // Use the user itself as the deleter (or create an admin). The user must exist in DB.
        var deletedBy = userId;
        await writeDac.DeleteAsync(userId, deletedBy, CancellationToken.None);

        // Assert
        var retrieved = await readDac.GetByIdAsync(userId, CancellationToken.None);
        retrieved.Should().BeNull("Should not retrieve soft-deleted user by default");
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
