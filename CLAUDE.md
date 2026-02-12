# Rgt.Space API - Microservices Base

## Architecture

Clean Architecture .NET 8 multi-tenant API for a POS/ERP platform (portal routing, identity, task allocation).

```
Rgt.Space.API            → Presentation (FastEndpoints, middleware, auth)
Rgt.Space.Core           → Domain (entities, abstractions, constants, read models)
Rgt.Space.Infrastructure → Application + Infra (CQRS, DACs, persistence, resilience, tenancy)
Rgt.Space.Tests          → xUnit (unit + integration with Testcontainers)
Rgt.Space.Schedulers     → Background jobs (separate project)
k6-Tests/                → Load/performance tests (k6)
READMEs/                 → Business rules, SQL schemas, task specs, architecture docs
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | .NET 8, ASP.NET Core |
| Endpoints | FastEndpoints (not controllers) |
| CQRS | MediatR 13 |
| Database | PostgreSQL 18 (single-database, row-level isolation) |
| ORM | Dapper (raw SQL, no EF Core) |
| Resilience | Polly v8 (timeout → retry → circuit breaker) |
| Caching | IMemoryCache (connection strings) + Redis (prepared, minimal use) |
| Auth | Dual JWT: SSO (RSA-SHA256) + Local (HMAC-SHA256) |
| Validation | FluentValidation 12 |
| Results | FluentResults (Result<T>, not exceptions) |
| Mapping | Riok.Mapperly (compile-time, zero reflection) |
| Logging | Serilog (structured, with LogContext enrichment) |
| Testing | xUnit, NSubstitute, FluentAssertions, Bogus, Testcontainers.PostgreSql |
| Docker | docker-compose (Redis only, DB is external) |

## Business Domain (4 Bounded Contexts)

| Context | Scope | Purpose |
|---------|-------|---------|
| **Identity (IAM)** | Global | Users, auth, sessions, RBAC |
| **Tenancy (Catalog)** | Global/Client | Clients and project instances |
| **Gateway (Routing)** | Project | URL routing (client → project mapping) |
| **Resource (Allocation)** | Project | Task allocation (6-role staffing matrix) |

### The 6 Immutable Position Types
`TECH_PIC`, `TECH_BACKUP`, `FUNC_PIC`, `FUNC_BACKUP`, `SUPPORT_PIC`, `SUPPORT_BACKUP`

## Key Conventions

### IDs
- ALL primary keys use **UUIDv7** (`Uuid7.NewUuid7()`) — time-ordered, B-Tree friendly
- NEVER use `Guid.NewGuid()` (v4) or `gen_random_uuid()` for PKs

### Soft Deletes
- All entities have `IsDeleted`, `DeletedAt`, `DeletedBy` fields
- Queries MUST filter `WHERE is_deleted = FALSE`
- Unique constraints use **partial indexes**: `CREATE UNIQUE INDEX ... WHERE is_deleted = FALSE`

### Status Values
- Only `"Active"` or `"Inactive"` (use `StatusConstants.Active` / `StatusConstants.Inactive`)

### Timestamps
- Storage: `TIMESTAMP WITHOUT TIME ZONE` (UTC)
- Display: App converts to `Asia/Kuala_Lumpur`
- DB triggers auto-update `updated_at` on mutations

### Error Handling
- Handlers return `FluentResults.Result<T>` — not exceptions
- Error codes defined in `ErrorCatalog` (e.g., `USER_NOT_FOUND`, `ROLE_CODE_EXISTS`)
- `ErrorCatalog.GetStatusCode(errorCode)` maps to HTTP status
- All error responses follow RFC 7807 ProblemDetails

### Naming
- Queries: `Get*Query` — reads that return data
- Commands: `*Command` — writes (create, update, delete)
- DACs: `*ReadDac` (queries) vs `*WriteDac` (CUD) — never mixed
- Endpoints: `Endpoints/[Domain]/[Action]/Endpoint.cs`
- Routes: `/api/v1/{resource-plural}/{id?}` with typed GUIDs (`{userId:guid}`)

## Auth Architecture

**Dual JWT with smart scheme selection:**
- `SsoBearer` — RSA-SHA256, from external SSO/Azure AD, OIDC discovery
- `LocalBearer` — HMAC-SHA256, from `/api/v1/auth/login`
- `MultiScheme` — inspects token issuer to route to correct scheme

**JIT provisioning:** SSO tokens trigger `IIdentitySyncService.SyncOrGetUserAsync()` — creates/links local user on first login.

**Permission loading:** `PermissionLoadingMiddleware` fetches DB permissions after auth, adds as claims. Format: `MODULE.RESOURCE.ACTION` (e.g., `TASK_ALLOCATION.MEMBERS_DIST.VIEW`).

**RBAC formula:** `EffectiveAccess = (RolePermissions UNION Override_Allow) MINUS Override_Deny`

## Database

- **Single database**: `rgt_space_portal` (PostgreSQL) — all tenants in one DB
- **Row-level isolation**: via `client_id` foreign keys, NOT tenant middleware
- **No multi-DB tenancy**: Connection factory returns the same `PortalDb` connection string
- **Dapper**: Raw SQL with `CommandDefinition` (always propagate `CancellationToken`)

### Deletion Rules
| Action | Projects | Mappings | Assignments |
|--------|----------|----------|-------------|
| Delete Client | BLOCKED | N/A | N/A |
| Delete Project | Cascade | Cascade | Cascade |
| Delete Mapping | No impact | Deleted | No impact |
| Delete User | BLOCKED if assigned | N/A | BLOCKED |

## Build & Test

```bash
# Start Redis (required for health checks, optional for API functionality)
docker-compose up -d

dotnet build Rgt.Space.sln
dotnet test Rgt.Space.Tests/Rgt.Space.Tests.csproj
```

Integration tests require Docker (Testcontainers spins up PostgreSQL) or `ConnectionStrings__TestDb` env var for CI.

### Required Configuration
- `ConnectionStrings:PortalDb` — PostgreSQL connection to `rgt_space_portal`
- `ConnectionStrings:Redis` — Redis connection (default: `localhost:6379`)
- `Auth:Authority` — SSO broker URL (for OIDC discovery)
- `Auth:Audience` — JWT audience
- `LocalAuth:SigningKey` — HMAC key for local JWT (MUST be in user-secrets for dev)

## Pre-Implementation Verification Gate

Before coding from a spec (any `READMEs/Tasks/TASK-*` document), deploy a background verification agent to stress-test the spec against the live codebase. The agent MUST run in parallel with early implementation — not block it — but critical findings halt work.

### What the agent validates
1. **Pattern conformance** — Does the spec follow existing DAC, handler, entity, endpoint conventions?
2. **Integration surface** — Do proposed interfaces/contracts mesh with existing abstractions?
3. **Assumption freshness** — Are the spec's assumptions about caching, DI, auth, tenancy still true?
4. **Logic stress-test** — Edge cases, race conditions, truth table gaps, missing error paths
5. **Scope calibration** — Is the spec over/under-engineering for the project's current maturity?

### Rules
- Agent runs in BACKGROUND — do not block Phase 1 work waiting for results
- Agent may spawn sub-agents if the verification surface is too large
- Findings classified as **CRITICAL** (halt), **WARNING** (review before merge), or **NOTE** (track)
- CRITICAL findings stop implementation immediately — fix spec first, then resume
- Time budget: verification must produce initial findings within ~5 minutes

## Parallel Subagent Deployment

For multi-file tasks, deploy parallel subagents with file-level boundaries. Proven pattern:

### Swarming Protocol
1. **Step 0 (sequential)**: Build shared primitives — seed helpers, base classes, shared DTOs. Verify they compile.
2. **Step 1 (parallel)**: Deploy 2 agents with non-overlapping output files. Each imports shared code from Step 0.
3. **Step 2 (sequential)**: Build, run tests, fix integration seams.

### Rules
- **Max 2 agents** for this codebase — more causes file conflicts or artificial boundaries
- Each agent owns exactly **one output file** — no shared writes
- Shared infrastructure (test helpers, factories) built in Step 0, not by agents
- UUIDv7 test data: use `$"PREFIX_{id:N}".ToUpper()[..20]` (32-char entropy), NOT `id.ToString()[..8]` (timestamp collision risk)
- Agents get the full context they need (interfaces, SQL schema, conventions) in their prompt — ~60% will overlap between prompts, that's expected

### When to Swarm
- 2+ independent files to create (e.g., DAC tests + endpoint tests)
- Each file has clear boundaries (different namespace, different test pattern)
- Total work would take >10 min sequential

### When NOT to Swarm
- Single file changes
- Files that depend on each other's output
- Exploratory/research work (use Explore agent instead)

## Migration Load Order (TestDatabaseInitializer)

```
00 → 01 → 03 → 01a → 02 → 06 → 08 → 09 → 10 → 11 → 12 → 13
```

- **03 before 02**: Migration 02 seeds `position_types`, created in 03
- **01a before 02**: Seeds DevAdmin with hardcoded ID so 02's `ON CONFLICT (email) DO NOTHING` preserves it
- **Skip 05**: Obsolete (03 already has the changes), would crash if loaded
- **Skip 07**: Has ordering bug (references `updated_by` before 08 adds it)

## DevAdmin Contract

- Hardcoded ID: `019ac92a-de20-7793-b8df-b88a87ea4e34`
- Email: `admin@rgtspace.com`
- `CurrentUser` (JWT-based) is ACTIVE in Extensions.cs — `DevCurrentUser` only used in test overrides
- Migration `01a-seed-devadmin.sql` ensures this ID exists in test DB
- Migration 02 uses `uuid_generate_v7()` (random) — 01a must run first to "win" the email conflict

## Known Tech Debt

1. ~~**Tenant header spoofing**~~: FIXED (TASK-014) — JWT tid validated, mismatch → 403.
2. ~~**TenantResolutionMiddleware order**~~: FIXED (TASK-014) — runs after UseAuthentication().
3. ~~**RequireHttpsMetadata = false**~~: FIXED (TASK-014) — gated behind IsDevelopment().
4. ~~**CORS AllowAll**~~: FIXED (TASK-014) — environment-conditional, whitelist in prod.
5. **FluentAssertions pinned to v6**: Safety lock comment in Tests.csproj — v8 requires commercial license.
6. **Health check `/health`**: Exposes infrastructure details without auth (low risk for internal APIs).
7. **AppException messages**: May contain internal details in client-facing errors (needs audit).
