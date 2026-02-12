# Rgt.Space.Core — Domain Layer

The innermost layer. Pure business logic, no infrastructure dependencies. Contains entities, abstractions (interfaces), constants, read models, and error handling.

## Entity Hierarchy

```
Entity (base)
  └─ Guid Id (protected set)
  └─ Equals/GetHashCode by ID

AuditableEntity : Entity
  └─ CreatedAt, CreatedBy
  └─ UpdatedAt, UpdatedBy
  └─ IsDeleted, DeletedAt, DeletedBy
  └─ UpdateAudit(userId), Delete(userId)
```

**All domain entities inherit from `AuditableEntity`** except `PositionType` (lightweight, minimal audit fields).

## Factory Method Pattern

Every entity uses static factory methods — NEVER public constructors:

```csharp
// Creation (enforces invariants)
static User CreateFromSso(externalId, email, displayName, provider)
static User CreateManual(displayName, email, contactNumber, ...)

// Rehydration from DB (no validation)
static User Rehydrate(id, displayName, ..., isDeleted, ...)
```

New entities: always add `Create()` and `Rehydrate()` methods.

## Domain Models

| Entity | Aggregate Root | Key Fields |
|--------|---------------|------------|
| `User` | Yes | DisplayName, Email, IsActive, LocalLogin*, Sso*, Password* |
| `UserSession` | No (child of User) | RefreshToken, ExpiresAt, IsRevoked |
| `Role` | Yes | Name, Code, IsSystem, IsActive |
| `Module` | Yes | Name, Code, IsActive, SortOrder |
| `Resource` | No (child of Module) | ModuleId, Name, Code |
| `Action` | Yes | Name, Code |
| `Permission` | Yes | ResourceId, ActionId, Code |
| `UserRole` | No (junction) | UserId, RoleId |
| `RolePermission` | No (junction) | RoleId, PermissionId |
| `UserPermissionOverride` | No | UserId, PermissionId, IsAllowed, Reason |
| `Client` | Yes | Name, Code, Status |
| `Project` | Yes | ClientId, Name, Code, ExternalUrl, Status |
| `ClientProjectMapping` | No (child of Project) | ProjectId, RoutingUrl, Environment |
| `ProjectAssignment` | No | ProjectId, UserId, PositionCode |
| `PositionType` | Yes (lightweight) | Code (PK, not UUID), Name, SortOrder, Status |
| `Tenant` | Yes | Name, Code, ConnectionString, Status |
| `Feature` | Yes | Code, Name, Description, IsEnabled, Version (concurrency) |
| `ClientFeature` | No (junction) | ClientId, FeatureId, IsEnabled, ConfigJson |
| `UserFeatureOverride` | No | UserId, FeatureId, IsEnabled, Reason, ExpiresAt |

## Abstractions (Interfaces)

### Tenancy
- `ITenantProvider` — current tenant context (`string? Id`)
- `ITenantConnectionFactory` — `GetSqlConnectionStringAsync(tenantId, ct)`
- `ISystemConnectionFactory` — `GetConnectionStringAsync(ct)` (static, no tenancy)

### Identity
- `ICurrentUser` — from JWT claims: `Id`, `ExternalId`, `Email`, `TenantKey`, `IsAuthenticated`
- `IIdentitySyncService` — JIT provisioning: `SyncOrGetUserAsync(provider, externalId, email, name)`

### DAC Interfaces (Data Access Contracts)
All follow Read/Write separation:
- `IUserReadDac` / `IUserWriteDac`
- `IRoleReadDac` / `IRoleWriteDac`
- `IClientReadDac` / `IClientWriteDac`
- `IProjectReadDac` / `IProjectWriteDac`
- `IClientProjectMappingReadDac` / `IClientProjectMappingWriteDac`
- `IProjectAssignmentReadDac` / `ITaskAllocationWriteDac`
- `ISalesReadDac`
- `IDashboardReadDac`
- `IFeatureReadDac` / `IFeatureWriteDac`
- `IFeatureGate` — runtime feature evaluation: `IsEnabledAsync(code, clientId?, userId?)`

### Other
- `IAuditLogger` — `LogAsync(AuditEntry)`, non-blocking enqueue
- `ICheckpointTracker` / `IComboBreakRecorder` / `IComboMapProvider` — dev debugging

## Read Models vs Domain Models

- **Domain models**: Enforce business rules, used by write operations, rich behavior
- **Read models**: Flat `record` types, denormalized, optimized for queries, no logic
- **Write DACs** accept/return domain entities
- **Read DACs** return read models

Read models live in `ReadModels/` — examples: `UserReadModel`, `ClientReadModel`, `ProjectAssignmentReadModel`.

## Error Handling

### Exception Hierarchy
```
AppException (abstract, has ErrorCode)
  ├─ ValidationException (400, includes field errors)
  ├─ NotFoundException (404)
  ├─ ConflictException (409)
  └─ TenantException (various)
```

### ErrorCatalog
Centralized error code → HTTP status mapping. Key methods:
- `GetStatusCode(errorCode)` → HTTP status int
- `GetTitle(errorCode)` → human-readable title
- `IsValidationError(errorCode)` → bool
- `IsRecordableError(errorCode)` → bool (for combo-break debugger)

Common codes: `USER_NOT_FOUND`, `USER_EMAIL_EXISTS`, `ROLE_CODE_EXISTS`, `ROLE_IS_SYSTEM`, `INVALID_CREDENTIALS`, `CLIENT_NOT_FOUND`, `PROJECT_NOT_FOUND`, `ROUTING_URL_ALREADY_EXISTS`, `FEATURE_NOT_FOUND`, `FEATURE_CODE_EXISTS`, `FEATURE_VERSION_CONFLICT`

## Constants Reference

| Class | Purpose | Key Values |
|-------|---------|-----------|
| `StatusConstants` | Entity status | `Active`, `Inactive` |
| `HttpConstants.Headers` | Custom headers | `X-Correlation-Id`, `X-Tenant`, `X-Checkpoint-*` |
| `HttpConstants.ContextKeys` | HttpContext.Items keys | `CorrelationId`, `TenantId`, `UserId` |
| `SqlConstants.CommandTimeouts` | Dapper timeouts | MasterDb=1s, TenantDb=1s, AuditDb=3s |
| `SqlConstants.ErrorCodes` | PostgreSQL transient errors | `40001`, `40P01`, `08000`, etc. |
| `CacheConstants` | Cache key prefixes & TTLs | tenant:connectionstring (10m), sale (5m) |
| `FeatureFlagConstants` | Feature flag cache keys & durations | feature:all (5m), feature:client (5m), feature:user (5m) |
| `TaskAllocationConstants.Positions` | The 6 position types | `TECH_PIC`, `FUNC_PIC`, `SUPPORT_PIC` + backups |
| `RegexPattern` | Validation patterns | Phone, Email, Name, ICNumber |

## Utilities

- `Uuid7.NewUuid7()` — generates time-ordered UUIDv7 (use for ALL new entity IDs)
- `PasswordHasher.HashPassword(password)` → `(byte[] Hash, byte[] Salt)` using HMAC-SHA512
- `PasswordHasher.VerifyPassword(password, hash, salt)` — constant-time comparison
- JSON converters: `DecimalPrecisionConverter`, `TrimmingConverter`, `NullToDefaultConverter`
