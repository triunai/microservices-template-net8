using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Rgt.Space.Tests.Integration.Persistence;

public class PositionTypeIntegrationTests : IAsyncLifetime
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
    public async Task PositionType_ShouldHaveStatusColumnAndSupportCrud()
    {
        // Arrange
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var code = "TEST_POS";
        var name = "Test Position";
        var sortOrder = 100;
        var status = "Active";

        // Act - Insert
        var insertSql = @"
            INSERT INTO position_types (code, name, sort_order, status, created_at, updated_at)
            VALUES (@Code, @Name, @SortOrder, @Status, NOW(), NOW())";

        await conn.ExecuteAsync(insertSql, new { Code = code, Name = name, SortOrder = sortOrder, Status = status });

        // Act - Read
        var readSql = "SELECT * FROM position_types WHERE code = @Code";
        var position = await conn.QuerySingleOrDefaultAsync<PositionTypeRow>(readSql, new { Code = code });

        // Assert
        position.Should().NotBeNull();
        position!.code.Should().Be(code);
        position.status.Should().Be("Active");

        // Act - Update Status
        var updateSql = "UPDATE position_types SET status = 'Inactive' WHERE code = @Code";
        await conn.ExecuteAsync(updateSql, new { Code = code });

        var updatedPosition = await conn.QuerySingleOrDefaultAsync<PositionTypeRow>(readSql, new { Code = code });

        // Assert Update
        updatedPosition!.status.Should().Be("Inactive");
    }

    private sealed record PositionTypeRow(
        string code,
        string name,
        string? description,
        int sort_order,
        string status,
        DateTime created_at,
        DateTime updated_at);
}
