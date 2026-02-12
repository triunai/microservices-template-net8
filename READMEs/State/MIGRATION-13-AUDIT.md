# Migration 13 Deep Audit — Discovery Doc

> Agent-maintained. Updated iteratively during analysis.
> If the agent compacts/dies, this doc preserves all findings.

## Status: COMPLETE

## Audit Scope
1. Cross-check migration 13 against business rules (READMEs/BusinessRules/)
2. Cross-check against true DB schema (all migrations 00-12)
3. Verify ALL modules/resources/permissions coverage (not just FEATURES)
4. Verify SYS_ADMIN gets complete permission set
5. Verify ON CONFLICT clauses match constraint types (plain vs partial)
6. Gap analysis: what's missing from the RBAC seed that should be there?
7. Blindspot analysis: edge cases, race conditions, ordering issues

---

## A. Schema Constraint Compatibility

### A1. modules.code — VERIFIED
Migration 12 converts `modules_code_uk` (plain UNIQUE) to partial index `idx_modules_code_active ON modules(code) WHERE is_deleted = FALSE`.
Migration 13 line 35 uses: `ON CONFLICT (code) WHERE is_deleted = FALSE DO NOTHING`
PostgreSQL supports ON CONFLICT targeting partial indexes when the WHERE clause matches exactly.
**Verdict**: Correct. The WHERE clause matches the index predicate exactly.

### A2. resources.(module_id, code) — VERIFIED
Migration 12 converts `resources_module_code_uk` (plain UNIQUE) to partial index `idx_resources_module_code_active ON resources(module_id, code) WHERE is_deleted = FALSE`.
Migration 13 line 46 uses: `ON CONFLICT (module_id, code) WHERE is_deleted = FALSE DO NOTHING`
**Verdict**: Correct. WHERE clause matches index predicate exactly.

### A3. permissions.(resource_id, action_id) — VERIFIED
Migration 01 line 129: `CONSTRAINT permissions_resource_action_uk UNIQUE (resource_id, action_id)` — plain UNIQUE, NOT converted by migration 12. The `permissions` table has no `is_deleted` column.
Migration 13 line 68 uses: `ON CONFLICT (resource_id, action_id) DO NOTHING`
**Verdict**: Correct. Plain UNIQUE + bare ON CONFLICT.

### A4. users.email — VERIFIED
Migration 12 converts `users_email_uk` to partial index `idx_users_email_active ON users(email) WHERE is_deleted = FALSE`.
Migration 13 lines 91/96/102 use: `ON CONFLICT (email) WHERE is_deleted = FALSE DO UPDATE SET ...`
**Verdict**: Correct. WHERE clause matches index predicate exactly.

### A5. user_roles.(user_id, role_id) — VERIFIED
Migration 01 line 165: `CONSTRAINT user_roles_uk UNIQUE (user_id, role_id)` — plain UNIQUE, NOT converted by migration 12 (no `is_deleted` column on `user_roles`).
Migration 13 line 116 uses: `ON CONFLICT (user_id, role_id) DO NOTHING`
**Verdict**: Correct. Plain UNIQUE + bare ON CONFLICT.

### A6. role_permissions.(role_id, permission_id) — VERIFIED
Migration 01 line 155: `CONSTRAINT role_permissions_pk PRIMARY KEY (role_id, permission_id)` — composite PK.
Migration 13 line 127 uses: `ON CONFLICT (role_id, permission_id) DO NOTHING`
**Verdict**: Correct. PRIMARY KEY works with ON CONFLICT like UNIQUE.

### A7. ON CONFLICT + partial index compatibility — VERIFIED
PostgreSQL (since v9.5) supports `ON CONFLICT ... WHERE predicate` to target partial unique indexes. The WHERE clause must match the index predicate expression exactly. All three partial-index-targeting ON CONFLICT clauses in migration 13 use `WHERE is_deleted = FALSE` which matches the index predicates created by migration 12.

**Section A Summary**: All 6 ON CONFLICT clauses are correct. No schema constraint issues found.

---

## B. Complete Permission Coverage

### B1. Endpoint Permission Inventory

Comprehensive scan of ALL endpoints under `Rgt.Space.API/Endpoints/`:

#### Endpoints using `Permissions()`:
| Endpoint | Permission String | Source |
|----------|------------------|--------|
| Features/GetFeatures | `FEATURES.LIST.VIEW` | FeatureFlagConstants.Permissions.ListView |
| Features/GetFeatureById | `FEATURES.LIST.VIEW` | FeatureFlagConstants.Permissions.ListView |
| Features/GetClientFeatures | `FEATURES.LIST.VIEW` | FeatureFlagConstants.Permissions.ListView |
| Features/GetClientSubscriptions | `FEATURES.LIST.VIEW` | FeatureFlagConstants.Permissions.ListView |
| Features/GetFeatureUserOverrides | `FEATURES.LIST.VIEW` | FeatureFlagConstants.Permissions.ListView |
| Features/GetUserOverrides | `FEATURES.LIST.VIEW` | FeatureFlagConstants.Permissions.ListView |
| Features/CreateFeature | `FEATURES.GLOBAL.EDIT` | FeatureFlagConstants.Permissions.GlobalEdit |
| Features/UpdateFeature | `FEATURES.GLOBAL.EDIT` | FeatureFlagConstants.Permissions.GlobalEdit |
| Features/DeleteFeature | `FEATURES.GLOBAL.EDIT` | FeatureFlagConstants.Permissions.GlobalEdit |
| Features/UpsertClientFeature | `FEATURES.CLIENT.EDIT` | FeatureFlagConstants.Permissions.ClientEdit |
| Features/SetUserOverride | `FEATURES.OVERRIDE.INSERT` | FeatureFlagConstants.Permissions.OverrideInsert |
| Features/ClearUserOverride | `FEATURES.OVERRIDE.DELETE` | FeatureFlagConstants.Permissions.OverrideDelete |
| TaskAllocation/GetStaffingMatrix | `TASK_ALLOCATION.MEMBERS_DIST.VIEW` | TaskAllocationConstants (hardcoded string build) |

#### Endpoints using `AllowAnonymous()` (no permission check):
- Auth/Login, Health/*, Debugging/ComboBreakTest, Sales/GetById
- Features/EvaluateFeature, Features/BulkEvaluateFeatures
- Identity/UpdateUser (has `AllowAnonymous()` uncommented — TODO pending)

#### Endpoints with NO `Permissions()` and NO `AllowAnonymous()` (rely on default auth):
- ALL Portal Routing endpoints (CreateClient, GetClientById, GetAllClients, etc.) — 12+ endpoints
- ALL Identity endpoints except UpdateUser (CreateUser, GetUser, GetUsers, DeleteUser, GetUserPermissions, GrantPermission, RevokePermission, AssignRole, UnassignRole, GetUserRoles)
- ALL Roles endpoints (GetRoles, GetRole, CreateRole, UpdateRole, DeleteRole)
- Dashboard/GetStats (has commented-out AllowAnonymous)
- TaskAllocation/GetProjectAssignments, AssignUser, UnassignUser, UpdateAssignment (commented-out AllowAnonymous)

### B2. Permission String Reconstruction Verification — VERIFIED

The middleware constructs `{module.code}.{resource.code}.{ACTION}`:
```
PermissionLoadingMiddleware line 64: permissionCodes.Add($"{p.Module}.{p.SubModule}.VIEW");
```

For FEATURES permissions to work, the DB must contain:
- Module: `code = 'FEATURES'`
- Resources: `code = 'LIST'`, `'GLOBAL'`, `'CLIENT'`, `'OVERRIDE'`, `'EVALUATE'`
- Actions: `code = 'VIEW'`, `'INSERT'`, `'EDIT'`, `'DELETE'`

This produces: `FEATURES.LIST.VIEW`, `FEATURES.GLOBAL.EDIT`, etc. -- matches FeatureFlagConstants.

**Verdict**: Permission string construction chain is correct.

### B3. WARNING: Unused FEATURES permissions generated

Migration 13 generates 20 permissions (5 resources x 4 actions = 20). But only 6 permission strings are actually used by endpoints:
- `FEATURES.LIST.VIEW` (ListView)
- `FEATURES.GLOBAL.EDIT` (GlobalEdit)
- `FEATURES.CLIENT.EDIT` (ClientEdit)
- `FEATURES.OVERRIDE.INSERT` (OverrideInsert)
- `FEATURES.OVERRIDE.DELETE` (OverrideDelete)
- `FEATURES.EVALUATE.VIEW` (EvaluateView -- declared in constants but NOT used by any endpoint)

The remaining 14 permissions (LIST.INSERT, LIST.EDIT, LIST.DELETE, GLOBAL.VIEW, GLOBAL.INSERT, GLOBAL.DELETE, CLIENT.VIEW, CLIENT.INSERT, CLIENT.DELETE, OVERRIDE.VIEW, OVERRIDE.EDIT, EVALUATE.INSERT, EVALUATE.EDIT, EVALUATE.DELETE) are unused.

**Verdict**: **WARNING** — 14 of 20 FEATURES permissions are dead weight. Not a bug (they're harmless), but adds noise to the permission system. This follows the same pattern as migration 06 which generates ALL resource x action combos. The convention is intentional — generate the full Cartesian product now, use subset immediately, rest available for future endpoints.

### B4. WARNING: EvaluateView permission is declared but never enforced

`FeatureFlagConstants.Permissions.EvaluateView = "FEATURES.EVALUATE.VIEW"` is defined in `FeatureFlagConstants.cs` line 34, but:
- `EvaluateFeature/Endpoint.cs` uses `AllowAnonymous()` (line 14)
- `BulkEvaluateFeatures/Endpoint.cs` uses `AllowAnonymous()` (line 14)

No endpoint calls `Permissions(FeatureFlagConstants.Permissions.EvaluateView)`.

**Verdict**: **NOTE** — This is by design. Evaluate endpoints are `AllowAnonymous()` because the frontend needs to call them before the user's permission set is loaded. The `EvaluateView` constant exists for future use when auth is restored (per `MEMORY.md`: "14 feature flag endpoints all with AllowAnonymous -- auth restore pending"). Not a bug, but the constant is currently dead code.

### B5. WARNING: Majority of non-feature endpoints have NO permission checks

Many endpoints rely on default FastEndpoints auth (just authentication, no specific permission). They have no `Permissions()` call:
- Portal Routing: 12+ endpoints (CreateClient, UpdateClient, DeleteClient, CreateProject, etc.)
- Identity: 10+ endpoints (CreateUser, GetUser, GetUsers, etc.)
- Roles: 5 endpoints (CRUD)
- Dashboard: 1 endpoint
- TaskAllocation: 3 endpoints (except GetStaffingMatrix which has permission)

These endpoints require a valid JWT (not AllowAnonymous) but do NOT check specific RBAC permissions.

**Verdict**: **NOTE** — This is acknowledged tech debt. The existing modules (PORTAL_ROUTING, TASK_ALLOCATION, USER_MGMT) have modules/resources/permissions seeded in migration 02+06, and SYS_ADMIN gets all permissions via migration 13 Part 5. But the endpoints themselves don't enforce them yet. Only GetStaffingMatrix has a `Permissions()` call. This is a phased rollout — feature flag endpoints were the first to get proper permission checks.

---

## C. Business Rules Cross-Check

### C1. FEATURES module permissions vs FEATURE-FLAGS.md — VERIFIED

`READMEs/BusinessRules/FEATURE-FLAGS.md` Section 13 lists required permissions:
```
- FEATURES.LIST.VIEW
- FEATURES.GLOBAL.EDIT
- FEATURES.CLIENT.EDIT
- FEATURES.OVERRIDE.INSERT
- FEATURES.OVERRIDE.DELETE
```

Migration 13 creates module `FEATURES` with 5 resources (LIST, GLOBAL, CLIENT, OVERRIDE, EVALUATE) and generates all 20 cross-product permissions. The 5 required permissions from the business rules are all included.

Note: `FEATURES.EVALUATE.VIEW` is NOT in the business rules doc but IS in `FeatureFlagConstants.Permissions`. This is acceptable -- the evaluate endpoints are `AllowAnonymous()` per design (Section B4).

**Verdict**: All business-rule-required permissions are present.

### C2. SYS_ADMIN role vs UAM-Rules.md — VERIFIED

`READMEs/BusinessRules/UAM-Rules.md` Section 3 states:
- "A System Admin is an Admin for ALL Clients"
- Roles are global (not tenant-scoped)
- `is_system = TRUE` for system roles

Migration 02 creates SYS_ADMIN role: `(uuid_generate_v7(), 'System Administrator', 'SYS_ADMIN', 'Full access to all modules', TRUE, TRUE)`
Migration 13 Part 5 grants ALL permissions to SYS_ADMIN via: `CROSS JOIN permissions p WHERE r.code = 'SYS_ADMIN'`

This correctly implements "Full access to all modules" per business rules.

**Verdict**: Correct. SYS_ADMIN definition matches business rules.

### C3. SSO user creation pattern vs SSO-Architecture.md — VERIFIED

`READMEs/BusinessRules/SSO-Architecture.md` Section 4.2 (JIT Provisioning):
1. Cache lookup by `sub` -> LocalUserId
2. DB lookup by `external_id`
3. No match -> check by email -> link if found, or create new

Migration 13 creates users with `local_login_enabled = FALSE` (SSO only), no `sso_provider` or `external_id` set. This is correct because:
- The user records are pre-seeded "placeholders"
- When the user logs in via SSO, `IdentitySyncService.SyncOrGetUserAsync()` (line 120) finds the user by email, then calls `LinkSso()` to populate `sso_provider` and `external_id`
- The `ON CONFLICT (email) WHERE is_deleted = FALSE DO UPDATE SET is_active = TRUE, display_name = ...` ensures the user is activated even if they already exist

**Verdict**: Correct. Follows JIT provisioning pattern correctly.

### C4. Golden-Source.md — VERIFIED (deprecated)

`READMEs/BusinessRules/Golden-Source.md` is marked DEPRECATED, superseded by `Architecture-Current-State.md`. No conflicts found with migration 13.

### C5. DB-Golden-Source.md — VERIFIED

`READMEs/BusinessRules/DB-Golden-Source.md` confirms:
- UUIDv7 for all PKs (migration 13 uses `uuid_generate_v7()`)
- Partial indexes for zombie constraints (migration 13 uses correct WHERE clause)
- UTC timestamps (migration 13 doesn't set timestamps, relying on DB defaults which are correct after migration 12)

**Verdict**: No conflicts.

---

## D. Feature Flag Completeness

### D1. FeatureFlagConstants.Permissions — complete mapping — VERIFIED

All 6 permission constants in `Rgt.Space.Core/Constants/FeatureFlagConstants.cs`:

| Constant | Value | DB Module | DB Resource | DB Action | Used By Endpoint |
|----------|-------|-----------|-------------|-----------|-----------------|
| ListView | `FEATURES.LIST.VIEW` | FEATURES | LIST | VIEW | 6 GET endpoints |
| GlobalEdit | `FEATURES.GLOBAL.EDIT` | FEATURES | GLOBAL | EDIT | Create/Update/Delete Feature |
| ClientEdit | `FEATURES.CLIENT.EDIT` | FEATURES | CLIENT | EDIT | UpsertClientFeature |
| OverrideInsert | `FEATURES.OVERRIDE.INSERT` | FEATURES | OVERRIDE | INSERT | SetUserOverride |
| OverrideDelete | `FEATURES.OVERRIDE.DELETE` | FEATURES | OVERRIDE | DELETE | ClearUserOverride |
| EvaluateView | `FEATURES.EVALUATE.VIEW` | FEATURES | EVALUATE | VIEW | None (AllowAnonymous) |

All constants map correctly to DB module/resource/action combinations that migration 13 will create.

**Verdict**: Complete. All constants have backing DB records.

### D2. FeatureGate.cs — no permission dependency — VERIFIED

`Rgt.Space.Infrastructure/Services/Features/FeatureGate.cs` does NOT reference any permission constants. It evaluates feature flags based on the feature/client/user data model, not RBAC permissions. This is correct per business rules: "Feature flags are NOT an access control system" (FEATURE-FLAGS.md Section 3).

**Verdict**: Correct. Feature evaluation is independent of RBAC.

### D3. No hardcoded permission strings outside constants — VERIFIED

Searched all Feature endpoint files for hardcoded permission strings. All use `FeatureFlagConstants.Permissions.*` except:
- TaskAllocation/GetStaffingMatrix uses inline string building with TaskAllocationConstants (not a feature flag endpoint, so N/A)

**Verdict**: No hardcoded permission strings in feature endpoints. Good hygiene.

---

## E. Gap Analysis

### E1. WARNING: Migration 13 NOT in TestDatabaseInitializer

File: `Rgt.Space.Tests/Integration/TestDatabaseInitializer.cs`

The `RequiredFiles` array ends at migration 12:
```csharp
"READMEs/SQL/PostgreSQL/Migrations/12-era1-retrofit.sql"
```

Migration 13 is NOT included. This means:
- Integration tests will NOT have FEATURES module/resources/permissions
- Integration tests will NOT have the 3 admin users
- Integration tests will NOT have SYS_ADMIN role_permissions for FEATURES

**Impact**: Tests that validate permission-based access to feature endpoints will fail or be misleading. The permission middleware will return empty permissions for FEATURES resources.

**Verdict**: **WARNING** — Must add migration 13 to `TestDatabaseInitializer.RequiredFiles` after line for migration 12. Order matters: 13 must be AFTER 12 (depends on partial indexes), AFTER 06 (depends on actions table being populated), and AFTER 02 (depends on SYS_ADMIN role existing).

### E2. WARNING: DevAdmin (admin@rgtspace.com) does NOT get SYS_ADMIN role from migration 13

Migration 13 Part 4 assigns SYS_ADMIN role ONLY to 3 emails:
```sql
WHERE u.email IN ('khumeren@gmail.com', 'khumeren@rgtech.com.my', 'kent.tan@rgtech.com.my')
```

But migration 02 already assigns SYS_ADMIN to `admin@rgtspace.com`:
```sql
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id FROM users u, roles r
WHERE u.email = 'admin@rgtspace.com' AND r.code = 'SYS_ADMIN'
```

However, migration 02 does NOT generate role_permissions. Migration 06 generates permissions but does NOT assign them to roles.

Migration 13 Part 5 grants ALL permissions to SYS_ADMIN:
```sql
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.code = 'SYS_ADMIN'
```

This DOES include permissions for ALL modules (PORTAL_ROUTING, TASK_ALLOCATION, USER_MGMT, and FEATURES). So `admin@rgtspace.com` (DevAdmin) DOES get all permissions via its existing SYS_ADMIN role assignment from migration 02.

**Verdict**: **VERIFIED** — DevAdmin is covered. The SYS_ADMIN role gets ALL permissions from Part 5, and DevAdmin was already assigned SYS_ADMIN in migration 02. No gap.

### E3. NOTE: Pre-existing modules had NO role_permissions before migration 13

Before migration 13, the permission chain was:
- Migration 02: Creates modules (PORTAL_ROUTING, TASK_ALLOCATION, USER_MGMT), resources, SYS_ADMIN role, assigns admin to SYS_ADMIN
- Migration 06: Generates permissions (resource x action cartesian product)
- **MISSING**: No migration ever ran `INSERT INTO role_permissions` for those permissions

This means SYS_ADMIN had the role but ZERO permissions. The `GetPermissionsAsync` CTE would return empty results.

Migration 13 Part 5 fixes this for the first time by granting ALL permissions to SYS_ADMIN.

**Verdict**: **WARNING** — This is a significant fix hidden in migration 13. The comment says "Includes the new FEATURES permissions from Part 2" but it actually grants ALL permissions for ALL modules (PORTAL_ROUTING, TASK_ALLOCATION, USER_MGMT, and FEATURES). Before migration 13, SYS_ADMIN had zero effective permissions in the DB. This should be called out more prominently in migration comments.

### E4. SSO JIT sync race condition with migration 13 — VERIFIED SAFE

Scenario: User `khumeren@gmail.com` logs in via SSO before migration 13 runs.
- `IdentitySyncService.SyncOrGetUserAsync()` creates a new user row with a random UUIDv7 ID.
- Migration 13 runs later: `ON CONFLICT (email) WHERE is_deleted = FALSE DO UPDATE SET is_active = TRUE, display_name = 'Khumeren'`
- This updates the existing JIT-created user (sets active + updates display name). The user ID is preserved (JIT sync's ID wins).

Reverse scenario: Migration 13 runs first, user logs in later.
- Migration 13 creates user with `uuid_generate_v7()` ID.
- JIT sync finds user by email, links SSO identity. User ID preserved (migration's ID wins).

**Verdict**: Safe in both orderings. The `ON CONFLICT ... DO UPDATE` in migration 13 is idempotent. JIT sync finds by email and links. No data loss in either direction.

### E5. NOTE: Migration 13 depends on specific prior migrations

Migration 13 requires these to have run first:
1. **Migration 01**: Creates users, modules, resources, actions, permissions, roles, role_permissions, user_roles tables
2. **Migration 02**: Creates SYS_ADMIN role, seeds actions (VIEW, INSERT, EDIT, DELETE)
3. **Migration 06**: Not strictly required (migration 13 generates FEATURES permissions independently)
4. **Migration 12**: Converts UNIQUE constraints to partial indexes (migration 13's ON CONFLICT WHERE clauses depend on this)

If migrations run in the documented order (00, 01, 03, 01a, 02, 06, 08, 09, 10, 11, 12, 13), all dependencies are satisfied.

**Verdict**: Order dependencies are satisfied by the current load order.

---

## F. Blindspot Analysis

### F1. WARNING: GetPermissionsAsync does NOT filter by is_deleted on modules/resources

File: `Rgt.Space.Infrastructure/Persistence/Dac/Identity/UserReadDac.cs` line 414-453

The CTE joins:
```sql
JOIN permissions p ON ep.permission_id = p.id
JOIN resources r ON p.resource_id = r.id
JOIN modules m ON r.module_id = m.id
JOIN actions a ON p.action_id = a.id
```

There is NO `WHERE r.is_deleted = FALSE AND m.is_deleted = FALSE` filter. If a module or resource is soft-deleted, its permissions would STILL be returned by this query.

The only filter is `r.is_active = TRUE` on the `roles` table in the CTE.

Note: `permissions` and `actions` tables do NOT have `is_deleted` columns, so no filter needed there. But `modules` and `resources` DO have `is_deleted` and it's not checked.

**Verdict**: **WARNING** — If a module or resource is soft-deleted, its permissions will still appear in the user's effective permission set. This is unlikely to cause issues now (nobody is soft-deleting modules) but is a latent bug. Adding `AND m.is_deleted = FALSE AND r.is_deleted = FALSE` to the final SELECT would fix it.

### F2. NOTE: GetPermissionsAsync does NOT filter by modules.is_active or resources.is_active

The CTE checks `roles.is_active = TRUE` but does NOT check `modules.is_active` (which defaults to TRUE).

If a module's `is_active` is set to FALSE (e.g., to temporarily disable the FEATURES module), its permissions would STILL be loaded and enforced. There is no "module disable" path that would remove permissions.

**Verdict**: **NOTE** — The `is_active` flag on modules has no effect on permission loading. This is arguably by design (RBAC is separate from feature flags), but means there's no way to administratively disable a module's permissions via the `modules.is_active` column.

### F3. VERIFIED: GetPermissionsAsync handles multiple modules correctly

The SQL groups by `m.code, r.code` with `BOOL_OR` aggregation. Adding the FEATURES module with 5 new resources will add up to 5 additional rows to the result set (one per resource). The middleware iterates all rows and adds all matching permission claims.

**Verdict**: Correct. No query changes needed for new modules.

### F4. NOTE: role_permissions has no audit columns

The `role_permissions` table (migration 01 line 152-156) is a bare junction table:
```sql
role_id UUID NOT NULL, permission_id UUID NOT NULL, PRIMARY KEY (role_id, permission_id)
```

No `created_at`, `created_by`, `assigned_at`, or audit trail. When migration 13 bulk-inserts all permissions for SYS_ADMIN, there's no record of when this happened or who did it.

**Verdict**: **NOTE** — Minor schema gap. The `user_roles` table has `assigned_at` and `assigned_by_user_id`, but `role_permissions` has no equivalent. Not a bug, but means permission grants to roles are unaudited at the DB level.

### F5. WARNING: permissions.code format differs from middleware permission format

Migration 13 Part 2 generates permission codes as `r.code || '_' || a.code`:
- Example: `LIST_VIEW`, `GLOBAL_EDIT`, `CLIENT_EDIT`

But the middleware constructs permission strings as `m.code + '.' + r.code + '.' + a.code`:
- Example: `FEATURES.LIST.VIEW`, `FEATURES.GLOBAL.EDIT`

The `permissions.code` column value (`LIST_VIEW`) is NEVER used by the middleware. The middleware reconstructs the permission string from the joined module/resource/action codes using dots.

The `permissions.code` is only used for the `permissions_code_uk` UNIQUE constraint (dedup within the permissions table). No code reads `permissions.code` for authorization decisions.

**Verdict**: **VERIFIED** — Not a bug. The `permissions.code` column is an administrative label, not used for permission matching. The middleware correctly builds the `MODULE.RESOURCE.ACTION` string from JOINed codes. However, the format inconsistency (`LIST_VIEW` in DB vs `FEATURES.LIST.VIEW` in middleware) could confuse future developers debugging permission issues.

### F6. CRITICAL: Migration 02 module INSERT uses RETURNING, will fail on re-run

Migration 02 (line 27-38) uses:
```sql
INSERT INTO modules (id, name, code, sort_order)
VALUES (uuid_generate_v7(), 'Portal Routing', 'PORTAL_ROUTING', 1)
RETURNING id INTO v_mod_id;
```

This does NOT have an `ON CONFLICT` clause. If migration 02 is re-run (e.g., during manual DB setup), it will fail because:
- After migration 12, `modules.code` uses partial index `idx_modules_code_active`
- A plain INSERT without ON CONFLICT will violate this constraint

However, migration 13 uses the correct pattern:
```sql
INSERT INTO modules (...) VALUES (...) ON CONFLICT (code) WHERE is_deleted = FALSE DO NOTHING;
SELECT id INTO v_mod_id FROM modules WHERE code = 'FEATURES' AND is_deleted = FALSE;
```

**Verdict**: **NOTE** — Migration 02 is not idempotent, but this is a known issue with the Era 1 migration and doesn't affect migration 13. Migration 13 is correctly idempotent.

### F7. VERIFIED: Migration 13 is idempotent (safe to re-run)

All operations in migration 13 use either:
- `ON CONFLICT ... DO NOTHING` (modules, resources, permissions, user_roles, role_permissions)
- `ON CONFLICT ... DO UPDATE SET` (users)

Re-running migration 13 will:
1. Skip module creation (already exists)
2. Skip resource creation (already exists)
3. Skip permission generation (already exists)
4. Update user display_name and is_active (harmless)
5. Skip role assignment (already exists)
6. Skip permission grants (already exists)

**Verdict**: Correct. Migration 13 is safe to re-run any number of times.

---

## Summary Table

| Category | Critical | Warning | Note | Verified |
|----------|----------|---------|------|----------|
| A. Schema Constraints | 0 | 0 | 0 | 7 |
| B. Permission Coverage | 0 | 2 | 2 | 2 |
| C. Business Rules | 0 | 0 | 0 | 5 |
| D. Feature Flags | 0 | 0 | 0 | 3 |
| E. Gap Analysis | 0 | 2 | 2 | 2 |
| F. Blindspots | 0 | 2 | 3 | 3 |
| **TOTAL** | **0** | **6** | **7** | **22** |

---

## Critical Findings: NONE

No blocking issues found. Migration 13 is safe to run.

## Top Warnings (fix before merge)

1. **E1**: Migration 13 NOT in TestDatabaseInitializer — add it or tests won't have FEATURES permissions
2. **E3**: Migration 13 silently fixes a pre-existing gap (SYS_ADMIN had zero role_permissions before this) — document this
3. **F1**: GetPermissionsAsync does not filter soft-deleted modules/resources — latent bug
4. **F2**: modules.is_active has no effect on permission loading
5. **B3**: 14 of 20 generated FEATURES permissions are unused (by design, matches convention)
6. **B4**: EvaluateView permission constant declared but not enforced by any endpoint

## Action Items

1. **Add migration 13 to TestDatabaseInitializer.RequiredFiles** (after migration 12)
2. **Consider adding `AND m.is_deleted = FALSE AND r.is_deleted = FALSE` to GetPermissionsAsync** (defensive, future-proofing)
3. **Add migration comment clarifying** that Part 5 grants ALL permissions for ALL modules, not just FEATURES
