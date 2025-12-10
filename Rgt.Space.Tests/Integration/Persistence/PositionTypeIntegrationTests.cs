using Dapper;
using Npgsql;
using Rgt.Space.Tests.Integration.Fixtures;

namespace Rgt.Space.Tests.Integration.Persistence;

[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class PositionTypeIntegrationTests
{
    private readonly TestDbFixture _fixture;
    private string ConnectionString => _fixture.ConnectionString;

    public PositionTypeIntegrationTests(TestDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PositionType_ShouldHaveStatusColumnAndSupportCrud()
    {
        // Arrange
        using var conn = new NpgsqlConnection(ConnectionString);
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
