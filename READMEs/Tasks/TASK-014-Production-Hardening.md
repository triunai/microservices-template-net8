# TASK-014: Production Hardening

## Objective

Fix all security vulnerabilities, auth gaps, and configuration issues before production deployment. The audit found **5 CRITICAL, 4 HIGH, 5 MEDIUM** issues across CORS, auth, tenant isolation, endpoint permissions, debug leakage, and logging.

## Audit Source

Full audit performed 2026-02-12 by Opus exploration agent (57 tool calls, 206s). Findings cross-referenced against `Program.cs`, `TenantResolutionMiddleware.cs`, `appsettings.json`, and all 48 endpoint files.

---

## Wave 1: Quick CRITICALs (~25 min, sequential)

All config/startup changes in `Program.cs` and `appsettings.json`. No new files.

### 1.1 — CORS: Replace AllowAll with whitelist

**File:** `Program.cs:76-84`
**Current:**
```csharp
options.AddPolicy("AllowAll", policy =>
{
    policy.AllowAnyOrigin()
          .AllowAnyHeader()
          .AllowAnyMethod();
});
```
**Fix:** Environment-conditional CORS:
- **Development**: AllowAnyOrigin (keep for Swagger/local frontend)
- **Production**: Whitelist specific origins from config (`Cors:AllowedOrigins` array)
- Allow credentials + specific headers (`Authorization`, `X-Tenant`, `X-Correlation-Id`, `Content-Type`)
- Allow specific methods (`GET`, `POST`, `PUT`, `DELETE`, `OPTIONS`)

**Config addition** (`appsettings.json`):
```json
"Cors": {
  "AllowedOrigins": ["https://portal.rgtspace.com", "https://admin.rgtspace.com"]
}
```

**Also update** `Program.cs:401` — rename policy from `"AllowAll"` to `"Default"` or similar.

### 1.2 — RequireHttpsMetadata: Gate behind environment

**File:** `Program.cs:106`
**Current:** `options.RequireHttpsMetadata = false; // TODO: Set to true in production`
**Fix:**
```csharp
options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
```

### 1.3 — JWT Signing Key: Remove hardcoded fallback

**File:** `Program.cs:216-218`
**Current:**
```csharp
localAuthConfig["SigningKey"] ?? "YourSuperSecretLocalSigningKey_ChangeThisInProduction_MustBe32CharactersLong!"
```
**Fix:** Remove the `??` fallback. Throw on startup if `LocalAuth:SigningKey` is missing:
```csharp
var signingKey = localAuthConfig["SigningKey"]
    ?? throw new InvalidOperationException("LocalAuth:SigningKey must be configured. Use dotnet user-secrets for development.");
```

**File:** `appsettings.json:16`
**Current:** `"SigningKey": "YourSuperSecretLocalSigningKey_ChangeThisInProduction_MustBe32CharactersLong!"`
**Fix:** Remove the `SigningKey` entry entirely from `appsettings.json`. Must come from user-secrets (dev) or environment variables (prod). Add a comment in its place:
```json
"_SigningKey_NOTE": "Set via user-secrets (dev) or environment variable LocalAuth__SigningKey (prod)"
```

### 1.4 — ShowPII: Gate behind environment

**File:** `Program.cs:24`
**Current:** `Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;`
**Fix:**
```csharp
Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = builder.Environment.IsDevelopment();
```
Note: `builder` isn't available at line 24 (before `CreateBuilder`). Move this line AFTER `var builder = WebApplication.CreateBuilder(args);` (after line 28).

### 1.5 — DB Credentials: Clean appsettings.json

**File:** `appsettings.json:2-7`
**Current:** Hardcoded `Password=123456` in 4 connection strings.
**Fix:** Replace with placeholder values that force configuration via environment:
```json
"ConnectionStrings": {
    "PortalDb": "Host=localhost;Database=rgt_space_portal;Username=postgres;Password=SET_VIA_ENV_OR_SECRETS;",
    "TenantMaster": "Host=localhost;Database=tenant_master;Username=postgres;Password=SET_VIA_ENV_OR_SECRETS;",
    "RgtAuthPrototype": "Host=localhost;Database=rgt_auth_prototype;Username=postgres;Password=SET_VIA_ENV_OR_SECRETS;",
    "RgtAuthAudit": "Host=localhost;Database=rgt_auth_audit;Username=postgres;Password=SET_VIA_ENV_OR_SECRETS;",
    "Redis": "localhost:6379,abortConnect=false,connectRetry=1,connectTimeout=200,syncTimeout=200,asyncTimeout=200"
}
```
**Also:** Create `appsettings.Development.json` (git-ignored) with the dev passwords for local use. Verify `.gitignore` has `appsettings.Development.json`.

### 1.6 — Serilog: Fix auth log levels and environment label

**File:** `appsettings.json:125-126`
**Current:**
```json
"Microsoft.AspNetCore.Authentication": "Debug",
"Microsoft.IdentityModel": "Debug"
```
**Fix:** Change both to `"Warning"` in base config. Create override in `appsettings.Development.json` if verbose auth logging is needed in dev.

**File:** `appsettings.json:154`
**Current:** `"Environment": "Development"`
**Fix:** Remove hardcoded value. Should come from `ASPNETCORE_ENVIRONMENT` or be set per-environment config.

---

## Wave 2: Permission Rollout (~45 min, swarmable — 2 agents)

Add `Permissions()` to all 36 endpoints that currently have NONE, and fix 2 endpoints with incorrect `AllowAnonymous()`.

### Existing Permission Strings (from DB seeds)

**Modules & resources (migration 02 + 13):**

| Module | Resources | Permission Format |
|--------|-----------|-------------------|
| `PORTAL_ROUTING` | `CLIENT_NAV`, `ADMIN_ROUTING` | `PORTAL_ROUTING.CLIENT_NAV.VIEW` |
| `TASK_ALLOCATION` | `MEMBERS_DIST` | `TASK_ALLOCATION.MEMBERS_DIST.VIEW` |
| `USER_MGMT` | `USER_ACCOUNT`, `USER_ACCESS` | `USER_MGMT.USER_ACCOUNT.VIEW` |
| `FEATURES` | `LIST`, `GLOBAL`, `CLIENT`, `OVERRIDE`, `EVALUATE` | `FEATURES.LIST.VIEW` |

**Actions (migration 02):** `VIEW`, `INSERT`, `EDIT`, `DELETE`

### Permission Constants File (new)

Create `Core/Constants/PermissionConstants.cs` — centralized permission strings for all non-feature domains. Pattern follows `FeatureFlagConstants.Permissions`.

```csharp
namespace Rgt.Space.Core.Constants;

public static class PermissionConstants
{
    public static class PortalRouting
    {
        public const string ClientView = "PORTAL_ROUTING.CLIENT_NAV.VIEW";
        public const string ClientInsert = "PORTAL_ROUTING.CLIENT_NAV.INSERT";
        public const string ClientEdit = "PORTAL_ROUTING.CLIENT_NAV.EDIT";
        public const string ClientDelete = "PORTAL_ROUTING.CLIENT_NAV.DELETE";

        public const string RoutingView = "PORTAL_ROUTING.ADMIN_ROUTING.VIEW";
        public const string RoutingInsert = "PORTAL_ROUTING.ADMIN_ROUTING.INSERT";
        public const string RoutingEdit = "PORTAL_ROUTING.ADMIN_ROUTING.EDIT";
        public const string RoutingDelete = "PORTAL_ROUTING.ADMIN_ROUTING.DELETE";
    }

    public static class TaskAllocation
    {
        public const string View = "TASK_ALLOCATION.MEMBERS_DIST.VIEW";
        public const string Insert = "TASK_ALLOCATION.MEMBERS_DIST.INSERT";
        public const string Edit = "TASK_ALLOCATION.MEMBERS_DIST.EDIT";
        public const string Delete = "TASK_ALLOCATION.MEMBERS_DIST.DELETE";
    }

    public static class UserManagement
    {
        public const string AccountView = "USER_MGMT.USER_ACCOUNT.VIEW";
        public const string AccountInsert = "USER_MGMT.USER_ACCOUNT.INSERT";
        public const string AccountEdit = "USER_MGMT.USER_ACCOUNT.EDIT";
        public const string AccountDelete = "USER_MGMT.USER_ACCOUNT.DELETE";

        public const string AccessView = "USER_MGMT.USER_ACCESS.VIEW";
        public const string AccessInsert = "USER_MGMT.USER_ACCESS.INSERT";
        public const string AccessEdit = "USER_MGMT.USER_ACCESS.EDIT";
        public const string AccessDelete = "USER_MGMT.USER_ACCESS.DELETE";
    }
}
```

### Endpoint → Permission Mapping (38 changes)

**Identity (11 endpoints):**

| Endpoint | File | Permission |
|----------|------|-----------|
| CreateUser | `Identity/CreateUser/Endpoint.cs` | `UserManagement.AccountInsert` |
| GetUsers | `Identity/GetUsers/Endpoint.cs` | `UserManagement.AccountView` |
| GetUser | `Identity/GetUser/Endpoint.cs` | `UserManagement.AccountView` |
| UpdateUser | `Identity/UpdateUser/Endpoint.cs` | `UserManagement.AccountEdit` (**also remove AllowAnonymous!**) |
| DeleteUser | `Identity/DeleteUser/Endpoint.cs` | `UserManagement.AccountDelete` |
| GetUserPermissions | `Identity/GetUserPermissions/Endpoint.cs` | `UserManagement.AccessView` |
| GrantPermission | `Identity/GrantPermission/Endpoint.cs` | `UserManagement.AccessInsert` |
| RevokePermission | `Identity/RevokePermission/Endpoint.cs` | `UserManagement.AccessDelete` |
| AssignRole | `Identity/AssignRole/Endpoint.cs` | `UserManagement.AccessInsert` |
| UnassignRole | `Identity/UnassignRole/Endpoint.cs` | `UserManagement.AccessDelete` |
| GetUserRoles | `Identity/GetUserRoles/Endpoint.cs` | `UserManagement.AccessView` |

**Roles (5 endpoints):**

| Endpoint | File | Permission |
|----------|------|-----------|
| GetRoles | `Roles/GetRoles/Endpoint.cs` | `UserManagement.AccessView` |
| GetRole | `Roles/GetRole/Endpoint.cs` | `UserManagement.AccessView` |
| CreateRole | `Roles/CreateRole/Endpoint.cs` | `UserManagement.AccessInsert` |
| UpdateRole | `Roles/UpdateRole/Endpoint.cs` | `UserManagement.AccessEdit` |
| DeleteRole | `Roles/DeleteRole/Endpoint.cs` | `UserManagement.AccessDelete` |

**PortalRouting — Clients (6 endpoints):**

| Endpoint | File | Permission |
|----------|------|-----------|
| CreateClient | `PortalRouting/CreateClient/Endpoint.cs` | `PortalRouting.ClientInsert` |
| GetAllClients | `PortalRouting/GetAllClients/Endpoint.cs` | `PortalRouting.ClientView` |
| GetClientById | `PortalRouting/GetClientById/Endpoint.cs` | `PortalRouting.ClientView` |
| UpdateClient | `PortalRouting/UpdateClient/Endpoint.cs` | `PortalRouting.ClientEdit` |
| DeleteClient | `PortalRouting/DeleteClient/Endpoint.cs` | `PortalRouting.ClientDelete` |
| GetProjectsByClient | `PortalRouting/GetProjectsByClient/Endpoint.cs` | `PortalRouting.ClientView` |

**PortalRouting — Projects (5 endpoints):**

| Endpoint | File | Permission |
|----------|------|-----------|
| CreateProject | `PortalRouting/CreateProject/Endpoint.cs` | `PortalRouting.RoutingInsert` |
| GetAllProjects | `PortalRouting/GetAllProjects/Endpoint.cs` | `PortalRouting.RoutingView` |
| GetProjectById | `PortalRouting/GetProjectById/Endpoint.cs` | `PortalRouting.RoutingView` |
| UpdateProject | `PortalRouting/UpdateProject/Endpoint.cs` | `PortalRouting.RoutingEdit` |
| DeleteProject | `PortalRouting/DeleteProject/Endpoint.cs` | `PortalRouting.RoutingDelete` |

**PortalRouting — Mappings (4 endpoints):**

| Endpoint | File | Permission |
|----------|------|-----------|
| CreateMapping | `PortalRouting/CreateMapping/Endpoint.cs` | `PortalRouting.RoutingInsert` |
| GetAllMappings | `PortalRouting/GetAllMappings/Endpoint.cs` | `PortalRouting.RoutingView` |
| UpdateMapping | `PortalRouting/UpdateMapping/Endpoint.cs` | `PortalRouting.RoutingEdit` |
| DeleteMapping | `PortalRouting/DeleteMapping/Endpoint.cs` | `PortalRouting.RoutingDelete` |

**TaskAllocation (4 endpoints — GetStaffingMatrix already done):**

| Endpoint | File | Permission |
|----------|------|-----------|
| AssignUser | `TaskAllocation/AssignUser/Endpoint.cs` | `TaskAllocation.Insert` |
| UnassignUser | `TaskAllocation/UnassignUser/Endpoint.cs` | `TaskAllocation.Delete` |
| UpdateAssignment | `TaskAllocation/UpdateAssignment/Endpoint.cs` | `TaskAllocation.Edit` |
| GetProjectAssignments | `TaskAllocation/GetProjectAssignments/Endpoint.cs` | `TaskAllocation.View` |

**Dashboard (1 endpoint):**

| Endpoint | File | Permission |
|----------|------|-----------|
| GetStats | `Dashboard/GetStats/Endpoint.cs` | `PortalRouting.ClientView` |

> Decision: Dashboard shows cross-module stats. Using `PortalRouting.ClientView` as minimum bar since dashboard primarily displays client/project counts. Any user who can see clients can see the dashboard.

**Sales (1 endpoint — fix AllowAnonymous):**

| Endpoint | File | Permission |
|----------|------|-----------|
| GetById | `Sales/GetById/Endpoint.cs` | `PortalRouting.ClientView` (**remove AllowAnonymous**) |

> Decision: Sales data should not be anonymous. Minimum VIEW permission required.

### Test Updates

The `TestAuthHandler` currently only grants Feature Flag permissions. Must be updated to also grant all permissions from `PermissionConstants` so existing integration tests don't break.

**File:** `Tests/Helpers/TestAuthHandler.cs` (or wherever the handler lives)
- Add all `PermissionConstants.*` strings to the claims list
- Existing tests continue to work (superadmin has everything)

**File:** `ClientEndpointTests.cs`
- Already uses `TestAuthHandler` — should work after handler is updated

### Swarming Strategy

- **Agent 1**: Identity (11) + Roles (5) + Dashboard (1) + Sales (1) = 18 endpoint files + `PermissionConstants.cs`
- **Agent 2**: PortalRouting (15) + TaskAllocation (4) = 19 endpoint files + `TestAuthHandler.cs`
- **Step 0 (sequential)**: Create `PermissionConstants.cs`, verify build
- **Step 2 (sequential)**: Update `TestAuthHandler.cs`, build, run tests

---

## Wave 3: Tenant Fix (~30 min, sequential)

### 3.1 — Reorder TenantResolutionMiddleware AFTER auth

**File:** `Program.cs:392`
**Current position:** Step 2 (before `UseAuthentication` at step 6)
**Problem:** `context.User?.Identity?.IsAuthenticated` is always `false` because JWT hasn't been validated yet. The "JWT tid claim" path is dead code.

**Fix:** Move `app.UseMiddleware<TenantResolutionMiddleware>()` to AFTER `app.UseAuthentication()` and BEFORE `app.UseMiddleware<PermissionLoadingMiddleware>()`.

**New pipeline order:**
```
1. UseExceptionHandler
2. CorrelationIdMiddleware
3. ComboBreakHeadersMiddleware
4. UseRateLimiter              ← rate limiting stays (partitions by IP now — see 3.3)
5. RateLimitHeadersMiddleware
6. UseCors
7. UseSerilogRequestLogging
8. UseAuthentication           ← JWT validated
9. TenantResolutionMiddleware  ← NOW after auth, JWT tid claim works
10. PermissionLoadingMiddleware
11. UseAuthorization
12. UseFastEndpoints
```

**Impact on rate limiting:** Rate limiter currently partitions by `tenantId` from `HttpContext.Items`, which is set by `TenantResolutionMiddleware`. With the reorder, rate limiting runs BEFORE tenant is known, so it'll partition by `"Unknown"` for all requests. Two options:
- **Option A (simple):** Rate limit by IP instead of tenant. Change `GlobalLimiter` to use `RemoteIpAddress`.
- **Option B (keep tenant):** Move rate limiter AFTER tenant resolution (after step 9). Means unauthenticated flood hits auth middleware first.

> Recommend **Option A** for prod. IP-based rate limiting is more standard and can't be spoofed via headers.

### 3.2 — Validate X-Tenant against JWT tid claim

**File:** `TenantResolutionMiddleware.cs`

After auth succeeds, if both JWT `tid` claim AND `X-Tenant` header are present, they must match. If they don't match, reject the request (403):

```csharp
// After auth, if user is authenticated:
if (context.User?.Identity?.IsAuthenticated == true)
{
    var jwtTid = context.User.FindFirst("tid")?.Value;
    var headerTenant = context.Request.Headers[HttpConstants.Headers.Tenant].FirstOrDefault();

    if (!string.IsNullOrWhiteSpace(jwtTid))
    {
        // JWT tid is authoritative
        if (!string.IsNullOrWhiteSpace(headerTenant) && headerTenant != jwtTid)
        {
            // Spoofing attempt: header doesn't match JWT
            _logger.LogWarning("Tenant mismatch: JWT tid={JwtTid}, X-Tenant={HeaderTenant}",
                jwtTid, headerTenant);
            context.Response.StatusCode = 403;
            return; // Short-circuit
        }
        tenantCode = jwtTid;
        source = "JWT-tid-claim";
    }
}
```

### 3.3 — Remove query parameter tenant resolution

**File:** `TenantResolutionMiddleware.cs:62-69`
**Current:** Accepts `?tenantId=` query parameter as Priority 3.
**Fix:** Remove entirely. Tenant should only come from JWT (authenticated) or X-Tenant header (pre-auth/health checks). Query parameter is an unnecessary attack surface.

---

## Wave 4: Debug & Logging Cleanup (~20 min, sequential)

### 4.1 — Gate debug endpoints behind environment check

**File:** `Endpoints/Debugging/ComboBreakTestEndpoint.cs`
**Fix:** Add `IsDevelopment()` guard or use conditional endpoint registration so endpoint is not available in production.

**File:** `Endpoints/Audit/DecodeAuditPayloadEndpoint.cs`
**Fix:** Same pattern — gate behind `IsDevelopment()` or require authentication.

### 4.2 — Remove JWT claims logging

**File:** `Program.cs:153-155` (SSO OnTokenValidated)
**Current:** `logger.LogInformation("SSO Token Validated. Claims: {Claims}", string.Join(", ", claims ?? ...));`
**Fix:** Remove the claims dump entirely. Log only a safe identifier:
```csharp
logger.LogDebug("SSO Token Validated. Subject: {Subject}", subject);
```

**File:** `Program.cs:226-227` (Local OnTokenValidated)
**Same fix:** Remove claims dump, log only subject at Debug level.

### 4.3 — Clean up Serilog Properties

**File:** `appsettings.json:152-155`
**Fix:** Remove hardcoded `"Environment": "Development"` — should come from `ASPNETCORE_ENVIRONMENT`:
```json
"Properties": {
    "Application": "MicroservicesBase.API"
}
```

---

## Wave 5: MEDIUMs (if time, ~50 min)

### 5.1 — Pipeline Key Normalization (~20 min)

**Scope:** All DAC constructors in `Infrastructure/Persistence/Dac/`
**Fix:** Normalize all to `"PortalDb"` pipeline key. Remove `"System"` registration from `Extensions.cs`. Both use the same database — having two pipelines with different resilience settings fragments circuit breaker isolation.

**DACs currently using `"System"`:**
- `DashboardReadDac`
- `TaskAllocationWriteDac`
- `ProjectAssignmentReadDac`
- `UserReadDac`
- `UserWriteDac`

**Decision:** Use `TenantDb` resilience settings (1500ms timeout, 0.7 failure ratio) for all.

### 5.2 — Health Check Lockdown (~15 min)

- Keep `/health/live` and `/health/ready` anonymous (K8s probes)
- Require auth for `/health` (detailed) and `/health/tenant/{tenantName}`
- Consider removing `/health/tenant/{tenantName}` — exposes tenant enumeration + can leak connection string fragments in error messages

### 5.3 — AppException Message Review (~10 min)

Audit all `AppException` subclasses to ensure messages don't contain table names, SQL fragments, or internal IDs. These are always returned to clients regardless of environment.

### 5.4 — GetProjectAssignments Duplicate Route Bug (~5 min)

**File:** `TaskAllocation/GetProjectAssignments/Endpoint.cs`
**Bug:** `Configure()` registers the route twice (copy-paste error).
**Fix:** Remove the duplicate `Get(...)` line.

---

## Files Changed Summary

### Wave 1 (config)
| File | Change |
|------|--------|
| `Program.cs` | CORS conditional, RequireHttpsMetadata, signing key throw, ShowPII gate, claims logging, middleware reorder |
| `appsettings.json` | Remove signing key, replace DB passwords, fix log levels, remove env label, add CORS config |
| `appsettings.Development.json` | **NEW** — dev-only overrides (passwords, verbose logging) |

### Wave 2 (permissions)
| File | Change |
|------|--------|
| `Core/Constants/PermissionConstants.cs` | **NEW** — centralized permission strings |
| 38 endpoint files | Add `Permissions()` call in `Configure()` |
| `TestAuthHandler.cs` | Add all permission strings to claims |

### Wave 3 (tenant)
| File | Change |
|------|--------|
| `Program.cs` | Middleware reorder (TenantResolution after Auth) |
| `TenantResolutionMiddleware.cs` | JWT tid validation, remove query param fallback |

### Wave 4 (debug/logging)
| File | Change |
|------|--------|
| `ComboBreakTestEndpoint.cs` | Add env guard |
| `DecodeAuditPayloadEndpoint.cs` | Add env guard or require auth |
| `Program.cs` | Remove claims logging |

### Wave 5 (mediums, optional)
| File | Change |
|------|--------|
| 5 DAC files | Pipeline key `"System"` → `"PortalDb"` |
| `Extensions.cs` | Remove `"System"` pipeline registration |
| Health endpoint files | Auth/removal changes |
| `GetProjectAssignments/Endpoint.cs` | Remove duplicate route |

---

## Success Criteria

1. `dotnet build Rgt.Space.sln` — 0 errors
2. `dotnet test Rgt.Space.Tests/Rgt.Space.Tests.csproj` — 146/146 green (or more if new tests added)
3. Zero `AllowAnonymous()` on business endpoints (only Auth/Login, Health probes, Feature eval)
4. Zero hardcoded secrets in `appsettings.json`
5. `ShowPII = false` in production
6. `RequireHttpsMetadata = true` in production
7. CORS restricted to configured origins in production
8. Tenant resolution uses JWT `tid` claim when authenticated
9. Debug endpoints inaccessible in production
10. No PII in production logs at Information level

## Estimated Effort

| Wave | Time | Swarmable |
|------|------|-----------|
| Wave 1: CRITICALs | ~25 min | No (sequential, all Program.cs/config) |
| Wave 2: Permissions | ~45 min | Yes (2 agents, file-level boundaries) |
| Wave 3: Tenant Fix | ~30 min | No (middleware pipeline changes) |
| Wave 4: Debug/Logging | ~20 min | No (sequential, small changes) |
| Wave 5: MEDIUMs | ~50 min | Partially (DACs swarmable) |
| **Total** | **~2.5-3 hrs** | |
