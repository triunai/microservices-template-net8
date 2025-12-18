using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Core.Domain.Entities.Identity;
using Rgt.Space.Infrastructure.Persistence;
using Rgt.Space.Infrastructure.Persistence.Dac.Identity;
using Rgt.Space.Infrastructure.Resilience;
using Testcontainers.PostgreSql;
using FluentAssertions;

namespace Rgt.Space.Tests.Integration.Persistence;

/// <summary>
/// Integration tests for User DACs using Testcontainers for real PostgreSQL database.
/// This verifies the actual SQL queries work correctly against a real database.
/// </summary>
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

        // Create UUID v7 function and users table
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

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

            -- Create users table
            CREATE TABLE users (
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
            );";

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }

    private ResilienceSettings GetTestResilienceSettings()
    {
        return new ResilienceSettings
        {
            MasterDb = new PipelineSettings
            {
                TimeoutMs = 1000,
                RetryCount = 3,
                RetryDelaysMs = [100, 200, 300],
                FailureRatio = 0.5,
                SamplingDurationSeconds = 10,
                MinimumThroughput = 2,
                BreakDurationSeconds = 5
            }
        };
    }

    [Fact]
    public async Task CreateAndRetrieveUser_ShouldRoundTripSuccessfully()
    {
        // Arrange
        var connFactory = new TestConnectionFactory(_connectionString);
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.Id.Returns("test_tenant");
        
        var registry = new ResiliencePipelineRegistry<string>();
        var options = Options.Create(GetTestResilienceSettings());
        var logger = Substitute.For<ILogger<UserReadDac>>();

        var writeDac = new UserWriteDac(connFactory);
        var readDac = new UserReadDac(connFactory, registry, options, logger);

        var user = User.CreateFromSso("google_12345", "test@example.com", "Test User", "google");

        // Act
        var createdId = await writeDac.CreateAsync(user, CancellationToken.None);
        var retrievedUser = await readDac.GetByIdAsync(createdId, CancellationToken.None);

        // Assert
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Email.Should().Be(user.Email);
        retrievedUser.DisplayName.Should().Be(user.DisplayName);
        retrievedUser.SsoProvider.Should().Be(user.SsoProvider);
        retrievedUser.ExternalId.Should().Be(user.ExternalId);
    }

    [Fact]
    public async Task GetByExternalId_ShouldFindCorrectUser()
    {
        // Arrange
        var connFactory = new TestConnectionFactory(_connectionString);
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.Id.Returns("test_tenant");
        var registry = new ResiliencePipelineRegistry<string>();
        var options = Options.Create(GetTestResilienceSettings());
        var logger = Substitute.For<ILogger<UserReadDac>>();

        var writeDac = new UserWriteDac(connFactory);
        var readDac = new UserReadDac(connFactory, registry, options, logger);

        var user = User.CreateFromSso("azure_67890", "user@example.com", "Azure User", "azuread");
        await writeDac.CreateAsync(user, CancellationToken.None);

        // Act
        var retrievedUser = await readDac.GetByExternalIdAsync("azuread", "azure_67890", CancellationToken.None);

        // Assert
        retrievedUser.Should().NotBeNull();
        retrievedUser!.ExternalId.Should().Be("azure_67890");
        retrievedUser.SsoProvider.Should().Be("azuread");
    }

    [Fact]
    public async Task UpdateLastLogin_ShouldPersistToDatabase()
    {
        // Arrange
        var connFactory = new TestConnectionFactory(_connectionString);
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.Id.Returns("test_tenant");
        var registry = new ResiliencePipelineRegistry<string>();
        var options = Options.Create(GetTestResilienceSettings());
        var logger = Substitute.For<ILogger<UserReadDac>>();

        var writeDac = new UserWriteDac(connFactory);
        var readDac = new UserReadDac(connFactory, registry, options, logger);

        var user = User.CreateFromSso("ext_123", "update@example.com", "Update Test", "google");
        var userId = await writeDac.CreateAsync(user, CancellationToken.None);

        // Act
        await writeDac.UpdateLastLoginAsync(userId, "google", CancellationToken.None);
        var retrievedUser = await readDac.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        retrievedUser!.LastLoginAt.Should().NotBeNull();
        retrievedUser.LastLoginProvider.Should().Be("google");
    }

    private class TestConnectionFactory : ITenantConnectionFactory, ISystemConnectionFactory
    {
        private readonly string _connectionString;

        public TestConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public Task<string> GetSqlConnectionStringAsync(string tenantId, CancellationToken ct = default)
        {
            return Task.FromResult(_connectionString);
        }

        public Task<string> GetConnectionStringAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_connectionString);
        }
    }
}
