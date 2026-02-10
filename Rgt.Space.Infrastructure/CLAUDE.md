# Rgt.Space.Infrastructure — Application + Infrastructure Layer

Implements CQRS handlers, data access (Dapper), multi-tenancy, resilience (Polly), auditing, and DI registration. This is where `AddInfrastructure(configuration)` lives.

## CQRS Pattern (MediatR)

### Command/Query Structure
```csharp
// Query — sealed record implementing IRequest<Result<T>>
public sealed record GetSaleByIdQuery(Guid SaleId) : IRequest<Result<SaleResponse>>;

// Handler — sealed class, always returns FluentResults.Result<T>
public sealed class Handler : IRequestHandler<GetSaleByIdQuery, Result<SaleResponse>>
{
    public async Task<Result<SaleResponse>> Handle(GetSaleByIdQuery q, CancellationToken ct)
    {
        // 1. Validate
        // 2. Call DAC
        // 3. Map to response
        // 4. Return Result.Ok(response) or Result.Fail(errorCode)
    }
}
```

### Pipeline Behaviors (order matters)
1. `CheckpointPipelineBehavior` — tracks handler entry/exit for combo-break debugger
2. `AuditLoggingBehavior` — auto-audits all requests with sampling (10% reads, 100% writes)

Registered in `Extensions.cs`:
```csharp
cfg.AddOpenBehavior(typeof(CheckpointPipelineBehavior<,>));
cfg.AddOpenBehavior(typeof(AuditLoggingBehavior<,>));
```

## DAC Pattern (Data Access Components)

### Two DAC Types

**Pattern A — System database (static pipeline):**
```csharp
public sealed class UserReadDac : IUserReadDac
{
    private readonly ISystemConnectionFactory _connFactory;
    private readonly ResiliencePipeline _pipeline;  // Injected, pre-registered

    public UserReadDac(ISystemConnectionFactory connFactory,
        ResiliencePipelineProvider<string> pipelineProvider, ...)
    {
        _pipeline = pipelineProvider.GetPipeline("System");
    }
}
```
Used by: Identity DACs, Role DACs, Portal Routing DACs, Dashboard DAC

**Pattern B — Multi-tenant (dynamic pipeline per tenant):**
```csharp
public sealed class SalesReadDac : ISalesReadDac
{
    private readonly ITenantConnectionFactory _connFactory;
    private readonly ResiliencePipelineRegistry<string> _pipelineRegistry;
    // Pipeline created lazily per tenant, circuit breaker isolated per tenant
}
```
Used by: Sales DAC (future tenant-specific data)

### DAC Conventions
- Private sealed records for DB row mapping: `private sealed record _UserRow(...)`
- Snake_case fields matching database columns
- Always use `CommandDefinition` with explicit timeout and `CancellationToken`
- Always wrap in resilience pipeline: `await _pipeline.ExecuteAsync(async token => { ... }, ct)`
- Multi-result sets via `conn.QueryMultipleAsync(cmd)`

### Key DAC Files
```
Persistence/Dac/
├── SalesReadDac.cs
├── Identity/
│   ├── UserReadDac.cs, UserWriteDac.cs
│   └── RoleReadDac.cs, RoleWriteDac.cs
├── PortalRouting/
│   ├── ClientReadDac.cs, ClientWriteDac.cs
│   ├── ProjectReadDac.cs, ProjectWriteDac.cs
│   └── ClientProjectMappingReadDac.cs, ClientProjectMappingWriteDac.cs
├── TaskAllocation/
│   ├── ProjectAssignmentReadDac.cs
│   └── TaskAllocationWriteDac.cs
└── Features/
    ├── FeatureReadDac.cs
    └── FeatureWriteDac.cs
```

## Tenancy

### Connection Factory Chain
```
MasterTenantConnectionFactory (base — returns PortalDb connection)
  ↓ decorated by
CachedTenantConnectionFactoryWithStampedeProtection (IMemoryCache, 10-min TTL)
```

**In single-DB mode**, `GetSqlConnectionStringAsync(tenantId)` ignores tenantId and returns `PortalDb`.

**SystemConnectionFactory** — static connection to PortalDb, no tenancy concept. Used by Identity/Role DACs.

### Tenant Provider
`HeaderTenantProvider : ITenantProvider` — middleware sets via `SetTenant(string)`, DACs read via `.Id`.

### Cache Config
```csharp
services.AddMemoryCache(options => { SizeLimit = 1000, CompactionPercentage = 0.25 });
```

## Resilience (Polly v8)

### Pre-registered Pipelines
| Key | Used By | Timeout | Retries |
|-----|---------|---------|---------|
| `MasterDb` | Master tenant factory | 4000ms | 2 |
| `PortalDb` | Portal routing DACs | 4000ms | 2 |
| `System` | Identity/Role DACs | 4000ms | 2 |
| `AuditDb` | Audit logger | 3500ms | 1 |
| `Redis` | Distributed cache | 2000ms | 1 |

### Pipeline Strategy Stack (outer → inner)
1. **Timeout** — enforces total latency budget
2. **Retry** — jittered backoff for transient errors
3. **Circuit Breaker** — failure-ratio based, prevents cascade

### Error Classifiers
- `IsSqlTransientError(Exception)` — handles `NpgsqlException` SQL states (40001, 40P01, 08000, etc.), `TimeoutException`, `SocketException`
- `IsRedisTransientError(Exception)` — handles `RedisException` with timeout/connection messages

### Settings
All configurable via `ResilienceSettings` section in appsettings.json. Each pipeline has: `TimeoutMs`, `RetryCount`, `RetryDelaysMs[]`, `FailureRatio`, `SamplingDurationSeconds`, `MinimumThroughput`, `BreakDurationSeconds`.

## Auditing

### Flow
1. `AuditLoggingBehavior` (MediatR pipeline) captures context
2. Enqueues `AuditEntry` to bounded `Channel<AuditEntry>` (non-blocking)
3. `AuditLogger` (IHostedService) background writer flushes batches to DB
4. Fallback: if DB unavailable → writes to Serilog

### Key Settings
- Queue: capacity 1000, batch size 100, flush every 5s
- Sampling: 10% reads, 100% writes
- Payloads: compressed (gzip), PII masked (email, phone, card), max 256KB
- Backpressure: drops oldest when channel full, falls back to file logging

### AuditEntry Fields
WHO (TenantId, UserId, ClientId, IpAddress), WHAT (Action, EntityType, EntityId), WHEN (Timestamp, CorrelationId, RequestPath), RESULT (IsSuccess, StatusCode, ErrorCode, DurationMs), PAYLOADS (RequestData, ResponseData, Delta — gzipped bytes).

## Identity

### CurrentUser (Production)
**Currently COMMENTED OUT** in Extensions.cs. Reads from JWT claims via `IHttpContextAccessor`:
- `Id` → `x-local-user-id` claim (set by JIT sync or local token validation)
- `ExternalId` → `sub` claim
- `Email` → `email` claim

### DevCurrentUser (Development)
**Currently ACTIVE** in Extensions.cs (line 230). Hardcoded system admin: `Id = 019ac92a-de20-7793-b8df-b88a87ea4e34`, `Email = admin@rgtspace.com`
Returns this ID for ALL requests regardless of JWT — test helpers and seed scripts depend on this ID existing in the DB.

### IdentitySyncService (JIT Provisioning)
SSO flow: Find by ExternalId → Find by Email (reactivate if deleted) → Create new user.

### TokenService
Local JWT generation: HMAC-SHA256, configurable expiry (default 60min access, 7d refresh). Refresh tokens are 64-byte random base64.

## Mapping (Riok.Mapperly)

Compile-time code generation, zero runtime overhead:
```csharp
[Mapper]
public partial class SalesMapper
{
    public partial SaleResponse ToResponse(SaleReadModel source);
}
```
Registered as **singletons** (stateless). Manual mapping for calculated properties.

## DI Registration (`Extensions.cs`)

`AddInfrastructure(IConfiguration)` registers everything:
- Settings: `AuditSettings`, `ResilienceSettings`
- Resilience pipelines (5 static + dynamic registry)
- MediatR + pipeline behaviors
- All DACs (scoped)
- Tenancy factories (singleton)
- Identity services (scoped)
- Token service (singleton)
- Mappers (singleton)
- Audit logger (singleton + hosted service)
- Combo-break debugger (env-conditional: real in Dev, null in Prod)

## Combo-Break Debugger

Dev-only diagnostic tool for tracing request flows:
- `CheckpointTracker` (scoped) — maintains checkpoint history per request
- `TrackedDacExecutor` (scoped) — wraps DAC calls with checkpoint tracking
- `ComboBreakRecorder` (singleton, dev-only) — records checkpoint sequences
- Checkpoint naming: `handler:{Name}`, `step:{Name}`, `repo:{DAC}:{Method}` — LOW CARDINALITY only
