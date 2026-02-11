using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly;
using Polly.Registry;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.Utilities;
using Rgt.Space.Infrastructure.Persistence.Dac.Features;
using Rgt.Space.Tests.Integration.Fixtures;

namespace Rgt.Space.Tests.Integration.Persistence;

[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class FeatureDacIntegrationTests
{
    private readonly TestDbFixture _fixture;
    private string ConnectionString => _fixture.ConnectionString;

    public FeatureDacIntegrationTests(TestDbFixture fixture)
    {
        _fixture = fixture;
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static string UniqueCode()
    {
        // Must match CHECK: code = upper(code) AND code ~ '^[A-Z][A-Z0-9_]*$'
        // Max 50 chars. Start with "T" to guarantee letter-start.
        return $"T{Guid.NewGuid():N}"[..20].ToUpper();
    }

    private static async Task SeedTestUserAsync(string connectionString, Guid userId)
    {
        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO users (id, display_name, email, is_active)
            VALUES (@Id, 'Test Admin', @Email, TRUE)
            ON CONFLICT (email) WHERE is_deleted = FALSE DO NOTHING",
            new { Id = userId, Email = $"test-{userId:N}@example.com" });
    }

    private static async Task SeedTestClientAsync(string connectionString, Guid clientId, Guid createdBy)
    {
        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        // Use full N-format GUID (no hyphens) for uniqueness — UUIDv7 first 8 chars can collide within same ms
        await conn.ExecuteAsync(@"
            INSERT INTO clients (id, name, code, status, created_by, updated_by)
            VALUES (@Id, 'Test Client', @Code, 'Active', @CreatedBy, @CreatedBy)
            ON CONFLICT (id) DO NOTHING",
            new { Id = clientId, Code = $"TC_{clientId:N}".ToUpper()[..20], CreatedBy = createdBy });
    }

    private static FeatureWriteDac CreateWriteDac(string connectionString)
    {
        var connFactory = new TestSystemConnectionFactory(connectionString);
        var pipelineProvider = Substitute.For<ResiliencePipelineProvider<string>>();
        pipelineProvider.GetPipeline("PortalDb").Returns(ResiliencePipeline.Empty);
        var logger = Substitute.For<ILogger<FeatureWriteDac>>();
        return new FeatureWriteDac(connFactory, pipelineProvider, logger);
    }

    private static FeatureReadDac CreateReadDac(string connectionString)
    {
        var connFactory = new TestSystemConnectionFactory(connectionString);
        var pipelineProvider = Substitute.For<ResiliencePipelineProvider<string>>();
        pipelineProvider.GetPipeline("PortalDb").Returns(ResiliencePipeline.Empty);
        var logger = Substitute.For<ILogger<FeatureReadDac>>();
        return new FeatureReadDac(connFactory, pipelineProvider, logger);
    }

    // ──────────────────────────────────────────────
    // Write DAC Tests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CreateFeatureAsync_ShouldReturnGuid_WhenValidInput()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        // Act
        var returnedId = await writeDac.CreateFeatureAsync(
            featureId, code, "Test Feature", "A test description",
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        // Assert
        returnedId.Should().Be(featureId);

        // Verify the row exists in the database with raw SQL
        using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT code, name, description, is_active, requires_client, is_deleted FROM features WHERE id = @Id",
            new { Id = featureId });

        Assert.NotNull(row);
        ((string)row!.code).Should().Be(code);
        ((string)row!.name).Should().Be("Test Feature");
        ((string)row!.description).Should().Be("A test description");
        ((bool)row!.is_active).Should().BeTrue();
        ((bool)row!.requires_client).Should().BeFalse();
        ((bool)row!.is_deleted).Should().BeFalse();
    }

    [Fact]
    public async Task CreateFeatureAsync_ShouldThrowConflictException_WhenDuplicateCode()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var code = UniqueCode();

        // Create the first feature successfully
        await writeDac.CreateFeatureAsync(
            Uuid7.NewUuid7(), code, "First Feature", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        // Act & Assert — creating with the same code should throw ConflictException
        var act = () => writeDac.CreateFeatureAsync(
            Uuid7.NewUuid7(), code, "Duplicate Feature", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdateFeatureAsync_ShouldReturnTrue_WhenFeatureExists()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Original Name", "Original Desc",
            isActive: false, requiresClient: true, createdBy: userId,
            CancellationToken.None);

        // Act
        var result = await writeDac.UpdateFeatureAsync(
            featureId, "Updated Name", "Updated Desc",
            isActive: true, requiresClient: false, updatedBy: userId,
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        // Verify fields changed via raw SQL
        using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT name, description, is_active, requires_client FROM features WHERE id = @Id",
            new { Id = featureId });

        Assert.NotNull(row);
        ((string)row!.name).Should().Be("Updated Name");
        ((string)row!.description).Should().Be("Updated Desc");
        ((bool)row!.is_active).Should().BeTrue();
        ((bool)row!.requires_client).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateFeatureAsync_ShouldReturnFalse_WhenFeatureIsDeleted()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "To Delete", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        // Soft-delete the feature first
        await writeDac.SoftDeleteFeatureAsync(featureId, userId, CancellationToken.None);

        // Act — try to update a deleted feature (TOCTOU scenario)
        var result = await writeDac.UpdateFeatureAsync(
            featureId, "Should Not Update", null,
            isActive: false, requiresClient: false, updatedBy: userId,
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SoftDeleteFeatureAsync_ShouldReturnTrue_AndCascade()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        var clientId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);
        await SeedTestClientAsync(ConnectionString, clientId, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        // Create feature
        await writeDac.CreateFeatureAsync(
            featureId, code, "Cascade Test", null,
            isActive: true, requiresClient: true, createdBy: userId,
            CancellationToken.None);

        // Create client_feature subscription
        await writeDac.UpsertClientFeatureAsync(clientId, featureId, true, userId, CancellationToken.None);

        // Create user_feature_override
        await writeDac.SetUserOverrideAsync(userId, featureId, "FORCE_ON", "Testing cascade", userId, CancellationToken.None);

        // Act
        var result = await writeDac.SoftDeleteFeatureAsync(featureId, userId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Feature should be soft-deleted
        var featureDeleted = await conn.ExecuteScalarAsync<bool>(
            "SELECT is_deleted FROM features WHERE id = @Id", new { Id = featureId });
        featureDeleted.Should().BeTrue();

        // Client feature should be cascade soft-deleted
        var clientFeatureDeleted = await conn.ExecuteScalarAsync<bool>(
            "SELECT is_deleted FROM client_features WHERE feature_id = @FeatureId AND client_id = @ClientId",
            new { FeatureId = featureId, ClientId = clientId });
        clientFeatureDeleted.Should().BeTrue();

        // User override should be cascade hard-deleted (row gone)
        var overrideCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM user_feature_overrides WHERE feature_id = @FeatureId AND user_id = @UserId",
            new { FeatureId = featureId, UserId = userId });
        overrideCount.Should().Be(0);
    }

    [Fact]
    public async Task SoftDeleteFeatureAsync_ShouldReturnFalse_WhenAlreadyDeleted()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Delete Twice", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        // First delete succeeds
        var firstResult = await writeDac.SoftDeleteFeatureAsync(featureId, userId, CancellationToken.None);
        firstResult.Should().BeTrue();

        // Act — second delete should return false
        var secondResult = await writeDac.SoftDeleteFeatureAsync(featureId, userId, CancellationToken.None);

        // Assert
        secondResult.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertClientFeatureAsync_ShouldInsertNew_ThenUpdateExisting()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        var clientId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);
        await SeedTestClientAsync(ConnectionString, clientId, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Upsert Test", null,
            isActive: true, requiresClient: true, createdBy: userId,
            CancellationToken.None);

        // Act — first upsert (insert)
        await writeDac.UpsertClientFeatureAsync(clientId, featureId, true, userId, CancellationToken.None);

        // Assert — inserted with is_enabled = true
        var afterInsert = await readDac.GetClientFeatureAsync(clientId, featureId, CancellationToken.None);
        afterInsert.Should().NotBeNull();
        afterInsert!.IsEnabled.Should().BeTrue();
        afterInsert.ClientId.Should().Be(clientId);
        afterInsert.FeatureId.Should().Be(featureId);

        var originalId = afterInsert.Id;

        // Act — second upsert (update)
        await writeDac.UpsertClientFeatureAsync(clientId, featureId, false, userId, CancellationToken.None);

        // Assert — same row updated, is_enabled flipped
        var afterUpdate = await readDac.GetClientFeatureAsync(clientId, featureId, CancellationToken.None);
        afterUpdate.Should().NotBeNull();
        afterUpdate!.IsEnabled.Should().BeFalse();
        afterUpdate.Id.Should().Be(originalId, "should be the same row, not a new insert");
    }

    [Fact]
    public async Task SetUserOverrideAsync_ShouldUpsert()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Override Test", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        // Act — set FORCE_ON
        await writeDac.SetUserOverrideAsync(userId, featureId, "FORCE_ON", "Initial reason", userId, CancellationToken.None);

        // Assert
        var afterFirst = await readDac.GetUserOverrideAsync(userId, featureId, CancellationToken.None);
        afterFirst.Should().NotBeNull();
        afterFirst!.OverrideState.Should().Be("FORCE_ON");
        afterFirst.Reason.Should().Be("Initial reason");

        // Act — upsert to FORCE_OFF
        await writeDac.SetUserOverrideAsync(userId, featureId, "FORCE_OFF", "Changed reason", userId, CancellationToken.None);

        // Assert — same user+feature, state updated
        var afterSecond = await readDac.GetUserOverrideAsync(userId, featureId, CancellationToken.None);
        afterSecond.Should().NotBeNull();
        afterSecond!.OverrideState.Should().Be("FORCE_OFF");
        afterSecond.Reason.Should().Be("Changed reason");
    }

    [Fact]
    public async Task ClearUserOverrideAsync_ShouldHardDelete()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Clear Override Test", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        await writeDac.SetUserOverrideAsync(userId, featureId, "FORCE_ON", "Will be cleared", userId, CancellationToken.None);

        // Verify it exists
        var before = await readDac.GetUserOverrideAsync(userId, featureId, CancellationToken.None);
        before.Should().NotBeNull();

        // Act
        await writeDac.ClearUserOverrideAsync(userId, featureId, CancellationToken.None);

        // Assert — row should be completely gone (hard delete)
        var after = await readDac.GetUserOverrideAsync(userId, featureId, CancellationToken.None);
        after.Should().BeNull();

        // Double-check with raw SQL to confirm no row exists at all
        using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM user_feature_overrides WHERE user_id = @UserId AND feature_id = @FeatureId",
            new { UserId = userId, FeatureId = featureId });
        count.Should().Be(0);
    }

    // ──────────────────────────────────────────────
    // Read DAC Tests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetByCodeAsync_ShouldReturnFeature_WhenExists()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Read By Code", "desc",
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        // Act
        var result = await readDac.GetByCodeAsync(code, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(featureId);
        result.Code.Should().Be(code);
        result.Name.Should().Be("Read By Code");
        result.Description.Should().Be("desc");
        result.IsActive.Should().BeTrue();
        result.RequiresClient.Should().BeFalse();
        result.CreatedBy.Should().Be(userId);
        result.UpdatedBy.Should().Be(userId);
    }

    [Fact]
    public async Task GetByCodeAsync_ShouldReturnNull_WhenDeleted()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Will Be Deleted", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        await writeDac.SoftDeleteFeatureAsync(featureId, userId, CancellationToken.None);

        // Act
        var result = await readDac.GetByCodeAsync(code, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFeature_WhenExists()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Read By Id", null,
            isActive: false, requiresClient: true, createdBy: userId,
            CancellationToken.None);

        // Act
        var result = await readDac.GetByIdAsync(featureId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(featureId);
        result.Code.Should().Be(code);
        result.Name.Should().Be("Read By Id");
        result.IsActive.Should().BeFalse();
        result.RequiresClient.Should().BeTrue();
        result.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        var readDac = CreateReadDac(ConnectionString);
        var randomId = Uuid7.NewUuid7();

        // Act
        var result = await readDac.GetByIdAsync(randomId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldExcludeDeletedFeatures()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);

        var keepId = Uuid7.NewUuid7();
        var keepCode = UniqueCode();
        var deleteId = Uuid7.NewUuid7();
        var deleteCode = UniqueCode();

        // Create two features
        await writeDac.CreateFeatureAsync(
            keepId, keepCode, "Keep Me", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        await writeDac.CreateFeatureAsync(
            deleteId, deleteCode, "Delete Me", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        // Soft-delete one
        await writeDac.SoftDeleteFeatureAsync(deleteId, userId, CancellationToken.None);

        // Act
        var result = await readDac.GetAllAsync(CancellationToken.None);

        // Assert
        // The DB has seed features (DASHBOARD_V2, SYS_COMBO_BREAK_DEBUG) plus the kept one.
        // We verify the kept feature IS in the list and the deleted one IS NOT.
        result.Should().Contain(f => f.Id == keepId);
        result.Should().NotContain(f => f.Id == deleteId);
    }

    [Fact]
    public async Task GetClientFeatureAsync_ShouldReturnSubscription()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        var clientId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);
        await SeedTestClientAsync(ConnectionString, clientId, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "Client Feature Read", null,
            isActive: true, requiresClient: true, createdBy: userId,
            CancellationToken.None);

        await writeDac.UpsertClientFeatureAsync(clientId, featureId, true, userId, CancellationToken.None);

        // Act
        var result = await readDac.GetClientFeatureAsync(clientId, featureId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ClientId.Should().Be(clientId);
        result.FeatureId.Should().Be(featureId);
        result.IsEnabled.Should().BeTrue();
        result.CreatedBy.Should().Be(userId);
        result.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task GetUserOverrideAsync_ShouldReturnOverride()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(
            featureId, code, "User Override Read", null,
            isActive: true, requiresClient: false, createdBy: userId,
            CancellationToken.None);

        await writeDac.SetUserOverrideAsync(userId, featureId, "FORCE_OFF", "Testing read", userId, CancellationToken.None);

        // Act
        var result = await readDac.GetUserOverrideAsync(userId, featureId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.FeatureId.Should().Be(featureId);
        result.OverrideState.Should().Be("FORCE_OFF");
        result.Reason.Should().Be("Testing read");
        result.CreatedBy.Should().Be(userId);
        result.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    // ──────────────────────────────────────────────
    // Read DAC Tests — TASK-010 List Queries
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetClientSubscriptionsByFeatureAsync_ShouldReturnSubscriptions()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        var clientId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);
        await SeedTestClientAsync(ConnectionString, clientId, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(featureId, code, "Sub Test", null, true, true, userId, CancellationToken.None);
        await writeDac.UpsertClientFeatureAsync(clientId, featureId, true, userId, CancellationToken.None);

        // Act
        var result = await readDac.GetClientSubscriptionsByFeatureAsync(featureId, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(r => r.ClientId == clientId && r.FeatureId == featureId && r.IsEnabled);
        var sub = result.First(r => r.ClientId == clientId);
        sub.ClientName.Should().NotBeNullOrWhiteSpace();
        sub.ClientCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetFeaturesByClientAsync_ShouldReturnFeatures()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        var clientId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);
        await SeedTestClientAsync(ConnectionString, clientId, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(featureId, code, "Client Feature Test", null, true, true, userId, CancellationToken.None);
        await writeDac.UpsertClientFeatureAsync(clientId, featureId, true, userId, CancellationToken.None);

        // Act
        var result = await readDac.GetFeaturesByClientAsync(clientId, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(r => r.FeatureId == featureId && r.FeatureCode == code);
        var feat = result.First(r => r.FeatureId == featureId);
        feat.FeatureName.Should().Be("Client Feature Test");
        feat.FeatureIsActive.Should().BeTrue();
        feat.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserOverridesByFeatureAsync_ShouldReturnOverrides()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(featureId, code, "Override By Feature", null, true, false, userId, CancellationToken.None);
        await writeDac.SetUserOverrideAsync(userId, featureId, "FORCE_ON", "Test reason", userId, CancellationToken.None);

        // Act
        var result = await readDac.GetUserOverridesByFeatureAsync(featureId, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(r => r.UserId == userId && r.FeatureId == featureId);
        var ov = result.First(r => r.UserId == userId);
        ov.OverrideState.Should().Be("FORCE_ON");
        ov.Reason.Should().Be("Test reason");
        ov.UserDisplayName.Should().NotBeNullOrWhiteSpace();
        ov.UserEmail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetOverridesByUserAsync_ShouldReturnOverrides()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);
        var featureId = Uuid7.NewUuid7();
        var code = UniqueCode();

        await writeDac.CreateFeatureAsync(featureId, code, "Override By User", null, true, false, userId, CancellationToken.None);
        await writeDac.SetUserOverrideAsync(userId, featureId, "FORCE_OFF", "User test", userId, CancellationToken.None);

        // Act
        var result = await readDac.GetOverridesByUserAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(r => r.FeatureId == featureId && r.FeatureCode == code);
        var ov = result.First(r => r.FeatureId == featureId);
        ov.OverrideState.Should().Be("FORCE_OFF");
        ov.Reason.Should().Be("User test");
        ov.FeatureName.Should().Be("Override By User");
    }

    // ──────────────────────────────────────────────
    // Read DAC Tests — TASK-011 Bulk Evaluation Queries
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetClientFeaturesByClientAsync_ShouldReturnSubscriptions()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        var clientId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);
        await SeedTestClientAsync(ConnectionString, clientId, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);

        var featureId1 = Uuid7.NewUuid7();
        var code1 = UniqueCode();
        var featureId2 = Uuid7.NewUuid7();
        var code2 = UniqueCode();

        await writeDac.CreateFeatureAsync(featureId1, code1, "Bulk Client Test 1", null, true, true, userId, CancellationToken.None);
        await writeDac.CreateFeatureAsync(featureId2, code2, "Bulk Client Test 2", null, true, true, userId, CancellationToken.None);
        await writeDac.UpsertClientFeatureAsync(clientId, featureId1, true, userId, CancellationToken.None);
        await writeDac.UpsertClientFeatureAsync(clientId, featureId2, false, userId, CancellationToken.None);

        // Act
        var result = await readDac.GetClientFeaturesByClientAsync(clientId, CancellationToken.None);

        // Assert
        result.Should().Contain(r => r.FeatureId == featureId1 && r.IsEnabled);
        result.Should().Contain(r => r.FeatureId == featureId2 && !r.IsEnabled);
        // Verify all results belong to the test client
        result.Where(r => r.ClientId == clientId).Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetUserOverridesByUserAsync_ShouldReturnOverrides()
    {
        // Arrange
        var userId = Uuid7.NewUuid7();
        await SeedTestUserAsync(ConnectionString, userId);

        var writeDac = CreateWriteDac(ConnectionString);
        var readDac = CreateReadDac(ConnectionString);

        var featureId1 = Uuid7.NewUuid7();
        var code1 = UniqueCode();
        var featureId2 = Uuid7.NewUuid7();
        var code2 = UniqueCode();

        await writeDac.CreateFeatureAsync(featureId1, code1, "Bulk Override Test 1", null, true, false, userId, CancellationToken.None);
        await writeDac.CreateFeatureAsync(featureId2, code2, "Bulk Override Test 2", null, true, false, userId, CancellationToken.None);
        await writeDac.SetUserOverrideAsync(userId, featureId1, "FORCE_ON", "Bulk test on", userId, CancellationToken.None);
        await writeDac.SetUserOverrideAsync(userId, featureId2, "FORCE_OFF", "Bulk test off", userId, CancellationToken.None);

        // Act
        var result = await readDac.GetUserOverridesByUserAsync(userId, CancellationToken.None);

        // Assert
        result.Should().Contain(r => r.FeatureId == featureId1 && r.OverrideState == "FORCE_ON");
        result.Should().Contain(r => r.FeatureId == featureId2 && r.OverrideState == "FORCE_OFF");
        // Verify all results belong to the test user
        result.Where(r => r.UserId == userId).Should().HaveCountGreaterThanOrEqualTo(2);
    }
}
