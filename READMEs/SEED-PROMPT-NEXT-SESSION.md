# Seed Prompt — Next Session Rehydration Context

Copy everything below this line and paste as your first message in a new Claude Code session.

---

## Hydration (read these first)

1. `READMEs/State/hot-state.md` — full project state, what's done, what's next
2. `MEMORY.md` (auto-loaded) — gotchas, patterns, conventions
3. `READMEs/State/MIGRATION-13-AUDIT.md` — deep audit report (0 critical, 6 warnings, 22 verified)

## Current State Snapshot

- **Branch**: `test/rbac-positiontype-verification-8034414594985432536`
- **Build**: 0 errors, 0 warnings
- **Tests**: 97 unit + 49 integration = 146 total, ALL GREEN
- **Auth**: `CurrentUser` (JWT-based) ACTIVE in production, `DevCurrentUser` only in test overrides
- **Migration load order**: `00 -> 01 -> 03 -> 01a -> 02 -> 06 -> 08 -> 09 -> 10 -> 11 -> 12 -> 13`
- **Feature flags**: 12 endpoints with `Permissions()`, 2 eval with `AllowAnonymous`
- **E2E tested**: Frontend SSO authentication confirmed working with feature flag endpoints

---

## RBAC Architecture — Verified Evidence (2026-02-11)

### 1. Permission String Construction — VERIFIED

`PermissionLoadingMiddleware` builds strings DYNAMICALLY from JOINed table data. It does NOT read `permissions.code`.

**Evidence** (`Rgt.Space.API/Middleware/PermissionLoadingMiddleware.cs` lines 60-68):
```csharp
foreach (var p in permissions)
{
    if (p.CanView) permissionCodes.Add($"{p.Module}.{p.SubModule}.VIEW");
    if (p.CanInsert) permissionCodes.Add($"{p.Module}.{p.SubModule}.INSERT");
    if (p.CanEdit) permissionCodes.Add($"{p.Module}.{p.SubModule}.EDIT");
    if (p.CanDelete) permissionCodes.Add($"{p.Module}.{p.SubModule}.DELETE");
}
```

The `p.Module` and `p.SubModule` come from `GetPermissionsAsync` (`UserReadDac.cs` lines 440-453):
```sql
SELECT m.code as module, r.code as sub_module,
    BOOL_OR(CASE WHEN a.code = 'VIEW' THEN TRUE ELSE FALSE END) as can_view,
    ...
FROM effective_permissions ep
JOIN permissions p ON ep.permission_id = p.id
JOIN resources r ON p.resource_id = r.id AND r.is_deleted = FALSE
JOIN modules m ON r.module_id = m.id AND m.is_deleted = FALSE
JOIN actions a ON p.action_id = a.id
GROUP BY m.code, r.code
```

So for FEATURES module: `m.code = 'FEATURES'`, `r.code = 'LIST'`, action VIEW -> produces `FEATURES.LIST.VIEW`.

The `permissions.code` column stores `LIST_VIEW` format — this is NEVER used for authorization. It's just a dedup label for the `permissions_code_uk` UNIQUE constraint.

### 2. Endpoint Permission Strings vs Middleware Output — ALL MATCH

| Endpoint | Checks For | Module.Resource.Action | Match |
|----------|-----------|----------------------|-------|
| GetFeatures, GetFeatureById, GetClientFeatures, GetClientSubscriptions, GetFeatureUserOverrides, GetUserOverrides | `FeatureFlagConstants.Permissions.ListView` = `FEATURES.LIST.VIEW` | FEATURES.LIST.VIEW | YES |
| CreateFeature, UpdateFeature, DeleteFeature | `FeatureFlagConstants.Permissions.GlobalEdit` = `FEATURES.GLOBAL.EDIT` | FEATURES.GLOBAL.EDIT | YES |
| UpsertClientFeature | `FeatureFlagConstants.Permissions.ClientEdit` = `FEATURES.CLIENT.EDIT` | FEATURES.CLIENT.EDIT | YES |
| SetUserOverride | `FeatureFlagConstants.Permissions.OverrideInsert` = `FEATURES.OVERRIDE.INSERT` | FEATURES.OVERRIDE.INSERT | YES |
| ClearUserOverride | `FeatureFlagConstants.Permissions.OverrideDelete` = `FEATURES.OVERRIDE.DELETE` | FEATURES.OVERRIDE.DELETE | YES |
| EvaluateFeature, BulkEvaluateFeatures | `AllowAnonymous()` — no permission check | N/A | N/A |
| TaskAllocation/GetStaffingMatrix | `TASK_ALLOCATION.MEMBERS_DIST.VIEW` (inline build from TaskAllocationConstants) | TASK_ALLOCATION.MEMBERS_DIST.VIEW | YES |

`EvaluateView` constant (`FEATURES.EVALUATE.VIEW`) exists in `FeatureFlagConstants` but is NOT enforced by any endpoint — eval endpoints are `AllowAnonymous` by design (frontend pre-auth).

### 3. RBAC Chain Completeness — FULLY SEEDED

Migration 13 (`13-seed-admin-accounts.sql`) creates the full chain:

| Layer | What | Created By |
|-------|------|-----------|
| `modules` | FEATURES (code='FEATURES', sort_order=10) | Migration 13 Part 1 |
| `resources` | LIST, GLOBAL, CLIENT, OVERRIDE, EVALUATE (under FEATURES) | Migration 13 Part 1 |
| `actions` | VIEW, INSERT, EDIT, DELETE | Migration 02 (pre-existing) |
| `permissions` | 20 rows (5 resources x 4 actions) | Migration 13 Part 2 |
| `roles` | SYS_ADMIN (code='SYS_ADMIN', is_system=TRUE) | Migration 02 (pre-existing) |
| `role_permissions` | SYS_ADMIN x ALL permissions (all modules) | Migration 13 Part 5 |
| `user_roles` | 3 admin users x SYS_ADMIN | Migration 13 Part 4 |

**KEY DISCOVERY**: Before migration 13, SYS_ADMIN had ZERO `role_permissions` rows. Migration 02 created the role, migration 06 generated permissions, but NOBODY ever linked them. Migration 13 Part 5 is the first migration to populate `role_permissions`.

### 4. Action Matrix — 4 Actions Confirmed

Migration 02 (`02-portal-seed.sql` lines 7-12) seeds exactly 4 actions:
```sql
INSERT INTO actions (id, name, code) VALUES
    (uuid_generate_v7(), 'View', 'VIEW'),
    (uuid_generate_v7(), 'Insert', 'INSERT'),
    (uuid_generate_v7(), 'Edit', 'EDIT'),
    (uuid_generate_v7(), 'Delete', 'DELETE')
ON CONFLICT (code) DO NOTHING;
```

Middleware hardcodes these 4 strings: `VIEW`, `INSERT`, `EDIT`, `DELETE` (lines 64-67).
FEATURES: 5 resources x 4 actions = 20 permissions. Correct.

### 5. ON CONFLICT vs Constraint Types — ALL VERIFIED

| Table | Constraint | Type | Migration 13 ON CONFLICT | Match |
|-------|-----------|------|-------------------------|-------|
| `modules(code)` | `idx_modules_code_active` | Partial index (migration 12) | `ON CONFLICT (code) WHERE is_deleted = FALSE` | YES |
| `resources(module_id, code)` | `idx_resources_module_code_active` | Partial index (migration 12) | `ON CONFLICT (module_id, code) WHERE is_deleted = FALSE` | YES |
| `permissions(resource_id, action_id)` | `permissions_resource_action_uk` | Plain UNIQUE (unchanged) | `ON CONFLICT (resource_id, action_id) DO NOTHING` | YES |
| `users(email)` | `idx_users_email_active` | Partial index (migration 12) | `ON CONFLICT (email) WHERE is_deleted = FALSE` | YES |
| `user_roles(user_id, role_id)` | `user_roles_uk` | Plain UNIQUE (no is_deleted) | `ON CONFLICT (user_id, role_id) DO NOTHING` | YES |
| `role_permissions(role_id, permission_id)` | `role_permissions_pk` | Composite PK | `ON CONFLICT (role_id, permission_id) DO NOTHING` | YES |

Tables WITHOUT `is_deleted`: `actions`, `permissions`, `roles`, `user_roles`, `role_permissions` -> keep plain UNIQUE.
Tables WITH `is_deleted`: `modules`, `resources`, `users` -> converted to partial indexes by migration 12.

### 6. SSO Account Semantics — VERIFIED SAFE

**JIT sync flow** (`IdentitySyncService.SyncOrGetUserAsync`):
1. Find by `(sso_provider, external_id)` -> if found, return
2. Find by `email` -> if found, link SSO identity (`LinkSso()` sets sso_provider + external_id)
3. Create new user

**Migration 13 seeds users WITHOUT sso_provider/external_id.** This is correct because:
- Step 1 won't match (no external_id set)
- Step 2 WILL match by email -> links SSO identity to the pre-seeded user
- The pre-seeded user already has SYS_ADMIN role -> permissions load immediately on first login

**Race condition**: Migration 13 uses `ON CONFLICT (email) WHERE is_deleted = FALSE DO UPDATE SET is_active = TRUE`. If JIT sync creates the user first, migration 13 updates it. If migration runs first, JIT sync finds by email and links. Safe in both orderings.

All 3 accounts: `local_login_enabled = FALSE` (SSO only, no local password).

### 7. Test Coverage for DB-seeded RBAC

**Current state**: Integration tests use `TestAuthHandler` which BYPASSES the DB permission chain entirely. It injects permission claims directly:
```csharp
// TestAuthHandler.cs — injects claims, never touches DB
new("permissions", FeatureFlagConstants.Permissions.ListView),
new("permissions", FeatureFlagConstants.Permissions.GlobalEdit),
// etc.
```

**Gap**: No integration test validates that the full DB chain (user -> user_roles -> role_permissions -> permissions -> resources -> modules -> middleware -> claims) produces the correct permission strings.

**Recommendation for next session**: Add an integration test that:
1. Uses REAL `PermissionLoadingMiddleware` (not TestAuthHandler)
2. Queries via the DevAdmin user (who gets SYS_ADMIN from migration 13)
3. Hits `GET /api/v1/features` and expects 200 (not 403)
4. This test will FAIL if migration 13 is removed or FEATURES RBAC is broken

### 8. Non-Feature Endpoints — Auth Gap Analysis

16 endpoints require JWT (not AllowAnonymous) but have NO `Permissions()` call. Any authenticated user can access them.

| Domain | Endpoints | Current Auth | Intended | Gap Severity |
|--------|-----------|-------------|----------|-------------|
| Identity | CreateUser, GetUser, GetUsers, DeleteUser, GetUserPermissions, GrantPermission, RevokePermission, AssignRole, UnassignRole, GetUserRoles | JWT only | USER_MGMT.USER_ACCOUNT.* / USER_ACCESS.* | P1 |
| Portal Routing | CreateClient, GetClientById, GetAllClients, UpdateClient, DeleteClient, + Projects + Mappings | JWT only | PORTAL_ROUTING.CLIENT_NAV.* / ADMIN_ROUTING.* | P1 |
| Roles | GetRoles, GetRole, CreateRole, UpdateRole, DeleteRole | JWT only | USER_MGMT.USER_ACCESS.* | P1 |
| Dashboard | GetStats | JWT only | TBD | P2 |
| TaskAllocation | AssignUser, UnassignUser, UpdateAssignment, GetProjectAssignments | JWT only | TASK_ALLOCATION.MEMBERS_DIST.* | P1 |
| Auth/Login | Login | AllowAnonymous | Correct | N/A |
| Health | 4 endpoints | AllowAnonymous | Correct | N/A |
| Feature Eval | 2 endpoints | AllowAnonymous | Correct (pre-auth) | N/A |

The RBAC data (modules, resources, permissions, role_permissions) for PORTAL_ROUTING, TASK_ALLOCATION, USER_MGMT already exists from migrations 02 + 06 + 13. The endpoints just need `Permissions()` calls added. This is Priority 2 for next session.

### 9. Cache + Propagation

**Permission cache** (`PermissionLoadingMiddleware.cs` lines 70-74):
```csharp
var cacheOptions = new MemoryCacheEntryOptions()
    .SetAbsoluteExpiration(TimeSpan.FromMinutes(1))  // 1-minute TTL
    .SetSize(1);
_cache.Set(cacheKey, permissionCodes, cacheOptions);
```

- **Where**: `IMemoryCache` (in-process, per-node)
- **TTL**: 1 minute absolute expiration
- **Key**: `Permissions:{userId}`
- **How to invalidate**: Restart API, or wait 60 seconds
- **Multi-node**: Each node has its own cache — stale for up to 60s on other nodes (known tech debt)
- **After running migration 13**: No API restart needed if you wait 60 seconds. Frontend confirmed working without restart.

### 10. Real-World Verification Checklist

#### SQL Checks (run via psql or any PostgreSQL client)

```sql
-- 1. Actions list (expect 4)
SELECT id, code, name FROM actions ORDER BY code;

-- 2. FEATURES module (expect 1 row)
SELECT id, code, name, sort_order FROM modules WHERE code = 'FEATURES';

-- 3. Resources under FEATURES (expect 5)
SELECT r.id, r.code, r.name FROM resources r
JOIN modules m ON r.module_id = m.id
WHERE m.code = 'FEATURES' AND r.is_deleted = FALSE;

-- 4. Permissions for FEATURES (expect 20)
SELECT COUNT(*) as total,
       string_agg(p.code, ', ' ORDER BY p.code) as codes
FROM permissions p
JOIN resources r ON p.resource_id = r.id
JOIN modules m ON r.module_id = m.id
WHERE m.code = 'FEATURES';

-- 5. SYS_ADMIN permission count (expect 20+ for FEATURES, more for other modules)
SELECT m.code as module, COUNT(*) as perm_count
FROM role_permissions rp
JOIN roles ro ON rp.role_id = ro.id
JOIN permissions p ON rp.permission_id = p.id
JOIN resources r ON p.resource_id = r.id
JOIN modules m ON r.module_id = m.id
WHERE ro.code = 'SYS_ADMIN'
GROUP BY m.code ORDER BY m.code;

-- 6. Your user's roles + effective permissions (replace email)
SELECT u.email, u.display_name, r.code as role_code,
       m.code || '.' || res.code || '.VIEW' as sample_permission
FROM users u
JOIN user_roles ur ON u.id = ur.user_id
JOIN roles r ON ur.role_id = r.id
JOIN role_permissions rp ON r.id = rp.role_id
JOIN permissions p ON rp.permission_id = p.id
JOIN resources res ON p.resource_id = res.id
JOIN modules m ON res.module_id = m.id
JOIN actions a ON p.action_id = a.id
WHERE u.email = 'khumeren@gmail.com' AND a.code = 'VIEW'
ORDER BY m.code, res.code;
```

#### API Checks

```bash
# 1. Get SSO token (login via frontend, copy from browser DevTools > Network > Authorization header)
# 2. Test protected endpoint
curl -H "Authorization: Bearer YOUR_TOKEN" https://localhost:60304/api/v1/features
# Expect: 200 + JSON array of features

# 3. Test anonymous endpoint (no token needed)
curl https://localhost:60304/api/v1/features/evaluate?userId=YOUR_GUID
# Expect: 200 + evaluation results

# 4. Test without token (should get 401)
curl https://localhost:60304/api/v1/features
# Expect: 401 Unauthorized
```

### The Killer Truth Test

**Claim**: "The exact string checked by Feature Flag endpoints is guaranteed to be present in the user's claims after migration 13, in production auth flow (not test auth)."

**Proof chain**:
1. Endpoint checks `FEATURES.LIST.VIEW` (from `FeatureFlagConstants.Permissions.ListView`)
2. FastEndpoints looks for claim type `"permissions"` with value `"FEATURES.LIST.VIEW"`
3. `PermissionLoadingMiddleware` adds this claim by constructing `$"{p.Module}.{p.SubModule}.VIEW"`
4. `p.Module` = `modules.code` = `'FEATURES'` (created by migration 13 Part 1)
5. `p.SubModule` = `resources.code` = `'LIST'` (created by migration 13 Part 1)
6. `p.CanView` = TRUE because `permissions` row exists linking resource LIST to action VIEW (migration 13 Part 2), and `role_permissions` links SYS_ADMIN to that permission (migration 13 Part 5), and user has SYS_ADMIN role (migration 13 Part 4)
7. `GetPermissionsAsync` CTE resolves: `user_roles` -> `role_permissions` -> `permissions` -> `resources(is_deleted=FALSE)` -> `modules(is_deleted=FALSE)` -> `actions`
8. User authenticated via SSO -> `OnTokenValidated` -> `SyncOrGetUserAsync` finds by email -> adds `x-local-user-id` claim -> middleware reads it -> loads permissions from DB -> adds `"permissions"` claims

**E2E confirmed**: Frontend SSO login -> `GET /api/v1/features` -> 200 with feature flag data. Working as of 2026-02-11.

---

## Next Session Priorities

1. **Phase 3: Entity/SQL alignment** — fix 11 entity/SQL mismatches, remove dead code (UserSession, Tenant entity)
2. **Permission rollout** — add `Permissions()` to 16 non-feature endpoints (Identity, PortalRouting, Roles, Dashboard, TaskAllocation)
3. **RBAC integration test** — add test that validates full DB permission chain (not TestAuthHandler bypass)
4. **Tooling setup** — hooks, MCP servers, custom skills
