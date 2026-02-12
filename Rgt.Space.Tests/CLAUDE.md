# Rgt.Space.Tests — Test Project

xUnit test suite with NSubstitute mocking, FluentAssertions, Bogus data generation, and Testcontainers for PostgreSQL integration tests.

## Test Stack

| Library | Version | Purpose |
|---------|---------|---------|
| xUnit | 2.9.2 | Test framework |
| NSubstitute | 5.3.0 | Mocking (interface-based) |
| FluentAssertions | 6.12.1 | Assertion library (pinned — v8 is commercial) |
| Bogus | 35.6.5 | Fake data generation |
| Testcontainers.PostgreSql | 4.2.0 | Docker-based integration DB |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.11 | WebApplicationFactory |

Global usings (auto-imported): `Xunit`, `FluentAssertions`, `NSubstitute`

## Naming Convention

```
[MethodName]_Should[ExpectedResult]_When[Condition]
```

Examples:
- `Create_ShouldInitializeWithDefaultStatusActive()`
- `Validate_ShouldRejectEmptyCode()`
- `SyncUserFromSsoAsync_WhenUserDoesNotExist_ShouldCreateNewUser()`

## Category Traits

All tests MUST have a category trait:
```csharp
[Trait("Category", "Unit")]        // Unit tests
[Trait("Category", "Integration")] // Integration tests
```

## AAA Pattern (Strict)

```csharp
[Fact]
[Trait("Category", "Unit")]
public void Create_ShouldInitializeWithDefaultStatusActive()
{
    // Arrange
    var code = "TECH_LEAD";
    var name = "Technical Lead";
    var sortOrder = 1;

    // Act
    var positionType = PositionType.Create(code, name, sortOrder);

    // Assert
    positionType.Should().NotBeNull();
    positionType.Code.Should().Be(code);
    positionType.Status.Should().Be(StatusConstants.Active);
}
```

## Unit Test Patterns

### NSubstitute Mocking
```csharp
public class IdentitySyncServiceTests
{
    private readonly IUserReadDac _userReadDac;
    private readonly IUserWriteDac _userWriteDac;
    private readonly ILogger<IdentitySyncService> _logger;
    private readonly IdentitySyncService _sut;  // System Under Test

    public IdentitySyncServiceTests()
    {
        _userReadDac = Substitute.For<IUserReadDac>();
        _userWriteDac = Substitute.For<IUserWriteDac>();
        _logger = Substitute.For<ILogger<IdentitySyncService>>();
        _sut = new IdentitySyncService(_userReadDac, _userWriteDac, _logger);
    }
}
```

Conventions:
- Substitutes created in constructor
- `_sut` for system under test
- `Arg.Is<T>(predicate)` for argument matching
- `Received(count).Method()` for call verification

### FluentAssertions
- `.Should().Be(value)` — equality
- `.Should().NotBeNull()` — null check
- `.Should().BeTrue()` / `.BeFalse()`
- `.Should().Throw<Exception>()` — exception
- `.Should().ContainSingle(e => ...)` — collection
- `.Should().BeOnOrAfter(date)` — temporal

## Integration Test Patterns

### Test Collection
```csharp
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<TestDbFixture> { }
```

All integration tests share the same `TestDbFixture` via `[Collection("IntegrationTests")]`.

### TestDbFixture (IAsyncLifetime)
```csharp
public class TestDbFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; }

    public TestDbFixture()
    {
        // Supports both CI (env var) and local (Testcontainers)
        var ciConnString = Environment.GetEnvironmentVariable("ConnectionStrings__TestDb");
        if (!string.IsNullOrWhiteSpace(ciConnString))
            ConnectionString = ciConnString;
        else
            _container = new PostgreSqlBuilder()
                .WithImage("public.ecr.aws/docker/library/postgres:15-alpine")
                .WithDatabase("test_db")
                .Build();
    }
}
```

### WebApplicationFactory Usage
```csharp
[Collection("IntegrationTests")]
public class ClientEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    public ClientEndpointTests(CustomWebApplicationFactory factory, TestDbFixture dbFixture)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISystemConnectionFactory>();
                services.AddSingleton<ISystemConnectionFactory>(
                    new TestSystemConnectionFactory(dbFixture.ConnectionString));
            });
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:PortalDb", dbFixture.ConnectionString },
                    { "ConnectionStrings:Redis", "localhost:6379" },
                    { "AuditSettings:Enabled", "false" }
                });
            });
        }).CreateClient();
    }
}
```

### Database Test Data
Integration tests use inline SQL INSERT via Dapper (not Bogus):
```csharp
using var conn = new NpgsqlConnection(ConnectionString);
await conn.OpenAsync();
await conn.ExecuteAsync("INSERT INTO position_types (...) VALUES (...)", new { ... });
```

Use `ON CONFLICT (id) DO NOTHING` (not bare `ON CONFLICT DO NOTHING`) — explicit conflict targets prevent silent swallows on partial unique indexes.

Result mapping uses private sealed records matching DB columns (snake_case):
```csharp
private sealed record PositionTypeRow(string code, string name, int sort_order, string status, ...);
```

### Test Data Isolation (CRITICAL)
Tests share the same Testcontainers DB. Ensure isolation via unique IDs/codes per test:
```csharp
// GOOD — full N-format GUID (32 chars of entropy)
var code = $"TC_{Guid.NewGuid():N}".ToUpper()[..20];

// BAD — UUIDv7 first 8 chars can collide within same millisecond
var code = $"TC_{clientId.ToString()[..8]}";
```

### Seeding Test Data for FK Constraints
Many tables have FK to `users(id)`. Always seed a user first:
```csharp
private static async Task SeedTestUserAsync(string connectionString, Guid userId)
{
    using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await conn.ExecuteAsync(@"
        INSERT INTO users (id, display_name, email, is_active)
        VALUES (@Id, 'Test Admin', @Email, TRUE)
        ON CONFLICT (email) DO NOTHING",
        new { Id = userId, Email = $"test-{userId:N}@example.com" });
}
```

### DAC Integration Test Pattern
```csharp
// Create DAC with mocked pipeline (ResiliencePipeline.Empty)
var connFactory = new TestSystemConnectionFactory(ConnectionString);
var pipelineProvider = Substitute.For<ResiliencePipelineProvider<string>>();
pipelineProvider.GetPipeline("PortalDb").Returns(ResiliencePipeline.Empty);
var logger = Substitute.For<ILogger<FeatureReadDac>>();
var dac = new FeatureReadDac(connFactory, pipelineProvider, logger);
```
NOTE: Pipeline key is `"PortalDb"` for ALL DACs (normalized in TASK-014).

### TestDatabaseInitializer
Loads SQL migration files in dependency order (NOT numeric). New features MUST add their migration here:
```csharp
private static readonly string[] RequiredFiles =
{
    "READMEs/SQL/PostgreSQL/Migrations/00-extensions.sql",
    "READMEs/SQL/PostgreSQL/Migrations/01-portal-schema.sql",
    "READMEs/SQL/PostgreSQL/Migrations/03-portal-routing-schema.sql",
    "READMEs/SQL/PostgreSQL/Migrations/01a-seed-devadmin.sql",
    "READMEs/SQL/PostgreSQL/Migrations/02-portal-seed.sql",
    "READMEs/SQL/PostgreSQL/Migrations/06-seed-permissions.sql",
    "READMEs/SQL/PostgreSQL/Migrations/08-fix-overrides-schema.sql",
    "READMEs/SQL/PostgreSQL/Migrations/09-feature-flags.sql",
    "READMEs/SQL/PostgreSQL/Migrations/10-feature-flag-seed.sql",
    "READMEs/SQL/PostgreSQL/Migrations/11-add-missing-fk-indexes.sql",
    "READMEs/SQL/PostgreSQL/Migrations/12-era1-retrofit.sql",
    "READMEs/SQL/PostgreSQL/Migrations/13-seed-admin-accounts.sql",
    "READMEs/SQL/PostgreSQL/Migrations/14-external-id-index.sql"
};
```

**Critical ordering rules:**
- 03 before 02 (02 seeds `position_types` created in 03)
- 01a before 02 (locks DevAdmin hardcoded ID before 02 generates random one)
- 14 after 12 (replaces compound index created in 12)
- Never load 05 (obsolete) or 07 (ordering bug)

## File Structure

```
Rgt.Space.Tests/
├── Unit/
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── PositionTypeTests.cs
│   │   │   └── UserTests.cs
│   │   └── Validators/
│   │       └── ClientValidatorTests.cs
│   ├── Services/
│   │   ├── IdentitySyncServiceTests.cs
│   │   └── FeatureGateTests.cs
│   └── Handlers/Features/
│       ├── CreateFeatureHandlerTests.cs
│       ├── UpdateFeatureHandlerTests.cs
│       ├── SoftDeleteFeatureHandlerTests.cs
│       ├── UpsertClientFeatureHandlerTests.cs
│       ├── SetUserOverrideHandlerTests.cs
│       ├── ClearUserOverrideHandlerTests.cs
│       ├── GetFeatureByIdHandlerTests.cs
│       └── GetAllFeaturesHandlerTests.cs
├── Integration/
│   ├── Api/
│   │   ├── ClientEndpointTests.cs
│   │   ├── FeatureEndpointTests.cs
│   │   ├── TenantResolutionTests.cs
│   │   ├── ConfigurableTestAuthHandler.cs
│   │   └── TestAuthHandler.cs
│   ├── Persistence/
│   │   ├── PositionTypeIntegrationTests.cs
│   │   ├── UserDacIntegrationTests.cs
│   │   └── FeatureDacIntegrationTests.cs
│   ├── Fixtures/
│   │   └── TestDbFixture.cs
│   ├── TestDatabaseInitializer.cs
│   ├── TestSystemConnectionFactory.cs
│   └── IntegrationTestCollection.cs
└── Utilities/
```

## Running Tests

```bash
# All tests
dotnet test Rgt.Space.Tests/Rgt.Space.Tests.csproj

# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests only (requires Docker for Testcontainers)
dotnet test --filter "Category=Integration"

# CI mode (provide connection string, skip Testcontainers)
set ConnectionStrings__TestDb=Host=localhost;Database=test_db;...
dotnet test
```
