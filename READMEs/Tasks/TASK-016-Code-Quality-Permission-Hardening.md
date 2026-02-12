# TASK-016: Code Quality + Permission Hardening

## Objective

Eliminate magic strings, make permission changes instantaneous, add bulk permission management, and close remaining entity/security gaps. Four independent waves targeting code quality, auth UX, architecture alignment, and security finishing.

## Audit Source

Collected from `state.md` remaining tech debt, `hot-state.md` backlog, and direct code audit (session 6, 2026-02-12). Magic string census performed via codebase-wide grep.

---

## Wave 1: Magic String Constants (~30 min, swarmable)

Create centralized constant classes for all repeated literal strings. Replace all uses across the codebase.

### 1.1 — Create `ClaimConstants.cs`

**File:** `Core/Constants/ClaimConstants.cs` (NEW)

```csharp
namespace Rgt.Space.Core.Constants;

/// <summary>
/// JWT and middleware claim type constants.
/// Used in Program.cs (auth events), PermissionLoadingMiddleware, CurrentUser, TestAuthHandler.
/// </summary>
public static class ClaimConstants
{
    /// <summary>FastEndpoints permission claim type. Added by PermissionLoadingMiddleware.</summary>
    public const string Permissions = "permissions";

    /// <summary>Tenant ID claim from SSO broker JWT.</summary>
    public const string TenantId = "tid";

    /// <summary>Local user ID claim. Added by OnTokenValidated for both SSO and Local schemes.</summary>
    public const string LocalUserId = "x-local-user-id";

    /// <summary>External identity provider claim from SSO broker (e.g., "Google", "AzureAD").</summary>
    public const string ExtProvider = "ext_provider";
}
```

**Replacement scope:**

| String | Occurrences (code only) | Key Files |
|--------|------------------------|-----------|
| `"permissions"` | ~30 | `PermissionLoadingMiddleware.cs`, `TestAuthHandler.cs`, `ConfigurableTestAuthHandler.cs`, `CurrentUser.cs` |
| `"tid"` | ~15 | `TenantResolutionMiddleware.cs`, `TestAuthHandler.cs`, `ConfigurableTestAuthHandler.cs`, `Program.cs` |
| `"x-local-user-id"` | ~8 | `Program.cs` (2), `PermissionLoadingMiddleware.cs`, `CurrentUser.cs`, `GlobalExceptionHandler.cs`, test handlers |
| `"ext_provider"` | ~5 | `Program.cs`, `IdentitySyncService.cs`, tests |

**Skip:** `"sub"` (standard OIDC claim, convention is literal), `"email"`, `"name"` (standard claims).

### 1.2 — Create `PipelineConstants.cs`

**File:** `Core/Constants/PipelineConstants.cs` (NEW)

```csharp
namespace Rgt.Space.Core.Constants;

/// <summary>
/// Polly resilience pipeline key constants.
/// All DACs use the same pipeline (single-database architecture).
/// </summary>
public static class PipelineConstants
{
    /// <summary>Primary database pipeline. Used by all DACs.</summary>
    public const string PortalDb = "PortalDb";
}
```

**Replacement scope:** ~20 occurrences across all DAC files + `Extensions.cs` + test mocks.

### 1.3 — Create `AuthSchemeConstants.cs`

**File:** `Core/Constants/AuthSchemeConstants.cs` (NEW)

```csharp
namespace Rgt.Space.Core.Constants;

/// <summary>
/// Authentication scheme name constants.
/// Used in Program.cs multi-scheme configuration.
/// </summary>
public static class AuthSchemeConstants
{
    public const string MultiScheme = "MultiScheme";
    public const string SsoBearer = "SsoBearer";
    public const string LocalBearer = "LocalBearer";
}
```

**Replacement scope:** ~8 occurrences in `Program.cs`.

### Swarming Strategy (Wave 1)

- **Agent 1:** Create 3 constant files + replace in `API/` project (Program.cs, middleware, endpoints)
- **Agent 2:** Replace in `Infrastructure/` (DACs, services) + `Tests/` (test handlers, mocks)
- **Step 0:** Create constant files, verify build
- **Step 2:** Build + test, verify 166/166 green

---

## Wave 2: Permission System Improvements (~45 min, sequential)

### 2.1 — Instantaneous Permission Cache

**Problem:** `PermissionLoadingMiddleware` caches permissions for 60 seconds (`TimeSpan.FromMinutes(1)`). When an admin grants/revokes permissions, the user doesn't see the change for up to 1 minute.

**File:** `API/Middleware/PermissionLoadingMiddleware.cs:71`
**Current:** `.SetAbsoluteExpiration(TimeSpan.FromMinutes(1))`

**Fix:** Cache-aside with explicit invalidation. Two changes:

**A) Add cache invalidation interface:**

```csharp
// Core/Abstractions/Identity/IPermissionCacheInvalidator.cs (NEW)
namespace Rgt.Space.Core.Abstractions.Identity;

public interface IPermissionCacheInvalidator
{
    void InvalidateUser(Guid userId);
}
```

**B) Implement in middleware (or a dedicated service):**

The `PermissionLoadingMiddleware` already uses `IMemoryCache`. Extract the cache key logic and expose an invalidation method. Register as singleton.

```csharp
// Infrastructure/Identity/PermissionCacheInvalidator.cs (NEW)
public sealed class PermissionCacheInvalidator(IMemoryCache cache) : IPermissionCacheInvalidator
{
    public void InvalidateUser(Guid userId)
    {
        cache.Remove($"Permissions:{userId}");
    }
}
```

**C) Call invalidation in permission-mutating handlers:**

| Handler | File | Invalidation |
|---------|------|-------------|
| `GrantPermissionHandler` | `Commands/Identity/GrantPermission.cs` | `_invalidator.InvalidateUser(command.UserId)` |
| `RevokePermissionHandler` | `Commands/Identity/RevokePermission.cs` | `_invalidator.InvalidateUser(command.UserId)` |
| `AssignRoleHandler` | `Commands/Identity/AssignRole.cs` | `_invalidator.InvalidateUser(command.UserId)` |
| `UnassignRoleHandler` | `Commands/Identity/UnassignRole.cs` | `_invalidator.InvalidateUser(command.UserId)` |

**Keep the cache TTL at 60s** as a safety net (stale data upper bound). The invalidation makes changes appear instantly; the TTL is backup.

**DI Registration:** `services.AddSingleton<IPermissionCacheInvalidator, PermissionCacheInvalidator>();`

### 2.2 — Bulk Permission Submission (PermissionMatrix)

**Problem:** Frontend's PermissionMatrix UI lets admins toggle multiple permissions at once, but the API only accepts one Module/SubModule pair per request. The frontend must send N sequential HTTP calls, each a separate DB transaction. Slow and fragile.

**New Endpoints:**

**A) Bulk Grant:**
```
POST /api/v1/users/{userId}/permissions/grant-bulk
Permission: USER_MGMT.USER_ACCESS.INSERT

Request Body:
{
    "permissions": [
        { "module": "PORTAL_ROUTING", "subModule": "CLIENT_NAV", "canView": true, "canInsert": false, "canEdit": true, "canDelete": false },
        { "module": "TASK_ALLOCATION", "subModule": "MEMBERS_DIST", "canView": true, "canInsert": true, "canEdit": true, "canDelete": true }
    ]
}

Response: 200 OK
{
    "granted": 2,
    "skipped": 0,
    "errors": []
}
```

**B) Bulk Revoke:**
```
POST /api/v1/users/{userId}/permissions/revoke-bulk
Permission: USER_MGMT.USER_ACCESS.DELETE

Request Body:
{
    "permissions": [
        { "module": "PORTAL_ROUTING", "subModule": "CLIENT_NAV" },
        { "module": "TASK_ALLOCATION", "subModule": "MEMBERS_DIST" }
    ]
}

Response: 200 OK
{
    "revoked": 2,
    "skipped": 0,
    "errors": []
}
```

**Implementation:**

| Layer | File | What |
|-------|------|------|
| Core | `Domain/Contracts/Identity/BulkGrantPermissionRequest.cs` | Request DTO with `List<PermissionEntry>` |
| Core | `Domain/Contracts/Identity/BulkRevokePermissionRequest.cs` | Request DTO |
| Core | `Domain/Contracts/Identity/BulkPermissionResponse.cs` | Response DTO |
| Infrastructure | `Commands/Identity/BulkGrantPermission.cs` | Command + Handler — single DB transaction, UPSERT loop |
| Infrastructure | `Commands/Identity/BulkRevokePermission.cs` | Command + Handler — single DB transaction, DELETE loop |
| Infrastructure | `Persistence/Dac/Identity/UserWriteDac.cs` | Add `BulkGrantPermissionsAsync` + `BulkRevokePermissionsAsync` methods |
| API | `Endpoints/Identity/BulkGrantPermission/Endpoint.cs` | POST endpoint |
| API | `Endpoints/Identity/BulkRevokePermission/Endpoint.cs` | POST endpoint |

**DB pattern:** Single connection, single transaction wrapping all UPSERT/DELETE operations. Rollback on any failure.

**Cache invalidation:** Call `_invalidator.InvalidateUser(userId)` once after the bulk operation completes.

**Validation:**
- Max 50 permissions per request (prevent abuse)
- Each Module/SubModule pair must exist in DB (validate via lookup)
- Duplicate entries in request → deduplicate silently

### 2.3 — UpdateUser Audit Trail Fix

**Problem:** `UpdateUser` handler uses `req.UserId` (the target user ID) for `updatedBy` instead of `ICurrentUser.Id` (the admin making the change). Now that auth is enforced, this should use the JWT identity.

**File:** Find the UpdateUser command handler
**Fix:** Change `updatedBy: command.UserId` → `updatedBy: command.UpdatedByUserId` (pass `ICurrentUser.Id` from endpoint)

---

## Wave 3: Entity/Architecture Alignment (~30 min, swarmable)

### 3.1 — TrackedEntity Base Class

**Problem:** Tables like `actions`, `permissions`, `roles` have NO soft-delete columns in SQL, but their C# entities inherit from `AuditableEntity` which includes `IsDeleted`, `DeletedAt`, `DeletedBy`. This is a lie — the DB doesn't support these fields for these tables.

**Fix:** Create a new base class `TrackedEntity` that has audit fields (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`) but NOT soft-delete fields.

**File:** `Core/Domain/Entities/TrackedEntity.cs` (NEW)

```csharp
namespace Rgt.Space.Core.Domain.Entities;

/// <summary>
/// Base entity with audit tracking but NO soft-delete.
/// Use for tables that don't have is_deleted/deleted_at/deleted_by columns.
/// </summary>
public abstract class TrackedEntity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public Guid CreatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }
}
```

**Entities to migrate:**

| Entity | Current Base | New Base | SQL Table |
|--------|-------------|----------|-----------|
| `Action` | `AuditableEntity` | `TrackedEntity` | `actions` (no soft-delete) |
| `Permission` | `AuditableEntity` | `TrackedEntity` | `permissions` (no soft-delete) |
| `Role` | `AuditableEntity` | `TrackedEntity` | `roles` (no soft-delete) |
| `RolePermission` | `AuditableEntity` | `TrackedEntity` | `role_permissions` (2-col junction) |

**Impact:** No runtime impact — these entities are only used in read operations (Dapper maps by column name, not by base class). But it's architectural honesty.

### 3.2 — Entity/SQL Alignment (Remaining Mismatches)

Fix the TODO markers left by TASK-013:

| Entity | Issue | Fix |
|--------|-------|-----|
| `Action` | Inherits AuditableEntity but SQL has no soft-delete | Switch to TrackedEntity |
| `Permission` | Same | Switch to TrackedEntity |
| `Role` | Same + SQL `code` is NOT NULL but entity has it optional | Switch to TrackedEntity, make `Code` required |
| `RolePermission` | SQL is 2-column junction (role_id, permission_id) only, entity has full audit fields | Switch to TrackedEntity or create junction-specific base |
| `UserRole` | SQL uses `assigned_by_user_id`, entity uses `CreatedBy` | Rename property or add alias |

---

## Wave 4: Security Finishing (~20 min, sequential)

### 4.1 — Health Check Auth

**File:** `Program.cs` (health check mappings)
**Current:** `/health` detailed endpoint is anonymous — exposes PostgreSQL and Redis connection status.
**Fix:** Keep `/health/live` and `/health/ready` anonymous (K8s probes need them). Add `RequireAuthorization()` to `/health` detailed endpoint.

### 4.2 — AppException Message Audit

**Scope:** All `AppException` and `FluentResults.Error` messages across handlers.
**Problem:** Some error messages may contain table names, SQL fragments, or internal IDs that leak to clients via ProblemDetails.
**Fix:** Audit all error messages. Replace any that contain internal details with generic user-facing messages. Keep internal details in logs only.

### 4.3 — Debug Endpoints Swagger Visibility

**Problem:** `ComboBreakTestEndpoint` and `DecodeAuditPayloadEndpoint` have `AllowAnonymous()` and appear in Swagger across all environments. The runtime `IsDevelopment()` guard returns 404 in prod, but the endpoints are still discoverable.
**Fix:** Use conditional endpoint registration so they don't appear in Swagger at all in non-dev:

```csharp
public override void Configure()
{
    // Only register in development
    if (!Env.IsDevelopment()) { DontRegister(); return; }
    // ... normal configuration
}
```

Or use FastEndpoints' `DontRegister()` method if running in non-dev environment.

---

## Files Changed Summary

### Wave 1 (constants)
| File | Change |
|------|--------|
| `Core/Constants/ClaimConstants.cs` | **NEW** — JWT/middleware claim constants |
| `Core/Constants/PipelineConstants.cs` | **NEW** — Polly pipeline key constants |
| `Core/Constants/AuthSchemeConstants.cs` | **NEW** — Auth scheme name constants |
| ~25 code files | Replace magic strings with constants |

### Wave 2 (permissions)
| File | Change |
|------|--------|
| `Core/Abstractions/Identity/IPermissionCacheInvalidator.cs` | **NEW** — cache invalidation interface |
| `Infrastructure/Identity/PermissionCacheInvalidator.cs` | **NEW** — IMemoryCache invalidation |
| `Core/Domain/Contracts/Identity/BulkGrantPermissionRequest.cs` | **NEW** — bulk grant DTO |
| `Core/Domain/Contracts/Identity/BulkRevokePermissionRequest.cs` | **NEW** — bulk revoke DTO |
| `Core/Domain/Contracts/Identity/BulkPermissionResponse.cs` | **NEW** — bulk response DTO |
| `Infrastructure/Commands/Identity/BulkGrantPermission.cs` | **NEW** — command + handler |
| `Infrastructure/Commands/Identity/BulkRevokePermission.cs` | **NEW** — command + handler |
| `API/Endpoints/Identity/BulkGrantPermission/Endpoint.cs` | **NEW** — POST endpoint |
| `API/Endpoints/Identity/BulkRevokePermission/Endpoint.cs` | **NEW** — POST endpoint |
| `Infrastructure/Persistence/Dac/Identity/UserWriteDac.cs` | Add bulk methods |
| 4 existing command handlers | Add `IPermissionCacheInvalidator` injection + invalidation call |
| `Infrastructure/Extensions.cs` | Register `IPermissionCacheInvalidator` |
| `Tests/Integration/Api/TestAuthHandler.cs` | Add new endpoint permissions |

### Wave 3 (entities)
| File | Change |
|------|--------|
| `Core/Domain/Entities/TrackedEntity.cs` | **NEW** — base class without soft-delete |
| `Core/Domain/Entities/Action.cs` | AuditableEntity → TrackedEntity |
| `Core/Domain/Entities/Permission.cs` | AuditableEntity → TrackedEntity |
| `Core/Domain/Entities/Role.cs` | AuditableEntity → TrackedEntity |
| `Core/Domain/Entities/RolePermission.cs` | AuditableEntity → TrackedEntity |
| `Core/Domain/Entities/UserRole.cs` | Fix `assigned_by` property alignment |

### Wave 4 (security)
| File | Change |
|------|--------|
| `Program.cs` | Health check auth on `/health` |
| Multiple handlers | AppException message audit |
| `ComboBreakTestEndpoint.cs` | Conditional registration |
| `DecodeAuditPayloadEndpoint.cs` | Conditional registration |

---

## Success Criteria

1. `dotnet build Rgt.Space.sln` -- 0 errors
2. `dotnet test` -- 166/166 green (or more with new bulk permission tests)
3. Zero magic strings for claim types, pipeline keys, or auth scheme names in code files
4. Permission changes take effect immediately (no 60-second wait)
5. Bulk permission endpoint handles N permissions in 1 HTTP call, 1 DB transaction
6. No entities claim soft-delete support when their SQL table doesn't have it
7. `/health` detailed endpoint requires auth
8. No internal details (table names, SQL) in client-facing error messages
9. Debug endpoints invisible in Swagger in non-dev environments

## Estimated Effort

| Wave | Time | Swarmable |
|------|------|-----------|
| Wave 1: Magic String Constants | ~30 min | Yes (2 agents: API+Core vs Infrastructure+Tests) |
| Wave 2: Permission Improvements | ~45 min | Partially (cache invalidation sequential, bulk endpoints swarmable) |
| Wave 3: Entity Alignment | ~30 min | Yes (TrackedEntity + entity fixes in parallel) |
| Wave 4: Security Finishing | ~20 min | No (sequential, small changes) |
| **Total** | **~2-2.5 hrs** | |

## Dependencies

- Wave 1 should run FIRST — Waves 2-4 can use the new constants
- Wave 2.1 (cache invalidation) must complete before 2.2 (bulk endpoints use the invalidator)
- Wave 3 and Wave 4 are independent of each other and Wave 2

## Test Plan

| Area | Tests |
|------|-------|
| Existing | 166/166 must stay green |
| Bulk Grant | +3 unit (happy path, validation, partial failure) |
| Bulk Revoke | +3 unit (happy path, validation, empty list) |
| Cache Invalidation | +2 unit (invalidate clears cache, grant triggers invalidation) |
| Bulk Integration | +4 endpoint integration (grant-bulk, revoke-bulk, mixed, 400 validation) |
| **New total** | ~12 new tests → **~178 total** |
