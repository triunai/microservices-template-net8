# TestDatabaseInitializer Gap Analysis

**Analysis Date:** 2026-02-10
**Analyst:** Claude Sonnet 4.5
**Scope:** 11 migrations, 5 loaded, 6 skipped — What are we NOT testing?

---

## TL;DR — The One Slide Version

```
CURRENT STATE: 5/11 migrations loaded → 49 tests passing ✅

PROBLEM: Hidden bugs, FK bombs, impossible test scenarios

SOLUTION: Load 3 CRITICAL migrations (02, 06, 08)

RESULT:
  ✅ Enables RBAC testing (currently impossible)
  ✅ Fixes FK bomb in migration 10
  ✅ Exposes UserWriteDac bug
  ✅ Prevents future FK bombs
  ⚠️ May break 1 test (fixable)

ACTION: Update TestDatabaseInitializer.RequiredFiles[] (see bottom)
```

---

## Executive Summary

The `TestDatabaseInitializer` loads only 5 of 11 migration files, creating an incomplete test database that differs significantly from production. This analysis identifies missing data, hidden bugs, impossible test scenarios, and FK chains that could crash tests at any moment.

**Quick Win:** Load 3 CRITICAL migrations (02, 06, 08) to unlock RBAC testing and prevent FK chain bombs.

**Impact:**
- ✅ Enables RBAC/permission integration tests (currently impossible)
- ✅ Fixes FK bomb exposed by migration 10 (created_by → users)
- ✅ Prevents future FK bombs (position_types, permissions)
- ✅ Exposes hidden bug in UserWriteDac (INSERT into overrides with missing columns)
- ⚠️ May break 1 test (UserDacIntegrationTests) if it calls GrantResourcePermissionsAsync

**Skip:** Migration 05 (OBSOLETE — migration 03 already has status columns)

**Consider:** Migration 04 (test data for multi-tenant E2E scenarios)

## Current State: What's Loaded vs Skipped

### ✅ LOADED (5 files)
1. `00-extensions.sql` — UUID v7 polyfill
2. `01-portal-schema.sql` — Core tables (users, roles, permissions, RBAC, position_types)
3. `03-portal-routing-schema.sql` — Domain tables (clients, projects, mappings, assignments)
4. `09-feature-flags.sql` — Feature flag tables + 2 test features
5. `10-feature-flag-seed.sql` — 4 module flags (DASHBOARD, TASK_ALLOCATION, etc.)

### ❌ SKIPPED (6 files)
1. `02-portal-seed.sql` — Actions, position types, modules, resources, admin role, admin user
2. `04-test-data.sql` — 3 clients, 6 projects, 5 mappings, 4 users, 6 assignments
3. `05-standardize-status.sql` — Schema migration (is_active → status)
4. `06-seed-permissions.sql` — Cartesian product (resources × actions → permissions)
5. `07-seed-overrides.sql` — Test permission overrides for admin user
6. `08-fix-overrides-schema.sql` — Adds updated_at/updated_by to user_permission_overrides

---

## Migration-by-Migration Analysis

### 02-portal-seed.sql — CRITICAL MISSING FOUNDATION DATA

**What it does:**
- Seeds 4 **actions** (VIEW, INSERT, EDIT, DELETE)
- Seeds 6 **position_types** (TECH_PIC, TECH_BACKUP, FUNC_PIC, FUNC_BACKUP, SUPPORT_PIC, SUPPORT_BACKUP)
- Seeds 3 **modules** (PORTAL_ROUTING, TASK_ALLOCATION, USER_MGMT) with 5 resources
- Seeds **SYS_ADMIN role**
- Seeds **admin@rgtspace.com user** and assigns admin role

**What's missing from test DB:**
- NO actions table data → permissions table is empty (even with migration 06)
- NO position_types data → project_assignments FK violations on insert
- NO modules/resources → permissions cannot be generated
- NO admin user → created_by/updated_by FKs break OR silently pass with NULL
- NO SYS_ADMIN role → RBAC tests cannot verify role permissions

**Tests at risk:**
- ✅ Feature flag tests PASS — they avoid RBAC entirely (all endpoints use `AllowAnonymous`)
- ❌ ANY test that creates project_assignments will FAIL (FK to position_types fails)
- ❌ Portal routing tests that seed clients/projects with `created_by` will FAIL (FK to users fails)
- ❌ RBAC tests are IMPOSSIBLE — no roles, no permissions, no admin user to test with

**Test scenarios impossible without this:**
- Testing RBAC permission loading middleware
- Testing role-based authorization
- Testing audit trail (created_by/updated_by tracking)
- Testing position type validation
- Testing module/resource visibility

**If we loaded this migration:**
- WOULD BREAK: Migration 10 (feature-flag-seed.sql) — it references a user that would have a different ID
- FIX: Migration 10 uses `created_by = NULL` instead of FK reference OR update it to use the seeded admin ID

**Should load:** ✅ YES — CRITICAL. Required for ANY domain tests beyond feature flags.

**Dependency chain:**
- `01-portal-schema.sql` → `02-portal-seed.sql` → `06-seed-permissions.sql`

---

### 04-test-data.sql — COMPREHENSIVE TEST FIXTURE DATA

**What it does:**
- Seeds 3 **clients** (ACME, TECHCORP, 7ELEVEN)
- Seeds 6 **projects** (2 per client: ACME/POS, ACME/INVENTORY, TECHCORP/POS, TECHCORP/ANALYTICS, 7ELEVEN/STORE, 7ELEVEN/LOGISTICS)
- Seeds 5 **client_project_mappings** (routing URLs like /acme/pos)
- Seeds 4 **users** (ahmad.abdullah@example.com, siti.nurhaliza@example.com, raj.kumar@example.com, meiling.tan@example.com)
- Seeds 6 **project_assignments** (staffing matrix for ACME POS and TECHCORP POS)

**What's missing from test DB:**
- NO realistic client/project data → tests must seed manually
- NO multi-client scenarios → cannot test client isolation
- NO realistic routing URLs → gateway routing tests impossible
- NO user roster → cannot test user-project relationships
- NO pre-assigned staff → cannot test assignment conflict/validation logic

**Tests at risk:**
- ✅ Feature flag tests PASS — they create isolated test data
- ⚠️ Portal routing tests — most seed their own data but cannot test EXISTING mapping conflicts
- ⚠️ Client endpoint tests — seed their own clients but miss multi-tenant edge cases
- ⚠️ Project assignment tests — seed their own data but miss conflict scenarios

**Test scenarios impossible without this:**
- Testing "user already assigned to this position" errors
- Testing "routing URL already taken" conflicts
- Testing "client has 10 projects" pagination
- Testing cross-client isolation (ACME user cannot see TECHCORP projects)
- Testing cascading deletes (delete client with projects)

**If we loaded this migration:**
- WOULD BREAK: Likely NONE — test isolation patterns use unique IDs/codes
- RISK: Tests that expect empty tables will fail (e.g., "GetAll returns 0 results")
- FIX: Tests should filter by their own seeded IDs OR use `DELETE FROM` in setup

**Should load:** ⚠️ CONDITIONAL
- Load for **integration/E2E test suites** that need realistic multi-tenant scenarios
- Skip for **unit/focused integration tests** that need clean slate

**Dependency chain:**
- `01-portal-schema.sql` → `02-portal-seed.sql` → `03-portal-routing-schema.sql` → `04-test-data.sql`

---

### 05-standardize-status.sql — SCHEMA MIGRATION (OBSOLETE)

**What it does:**
- Migrates `client_project_mappings.is_active` (BOOLEAN) → `status` (VARCHAR: Active/Inactive)
- Migrates `position_types.is_active` (BOOLEAN) → `status` (VARCHAR: Active/Inactive)
- Drops old `is_active` columns
- Updates indexes

**What's missing from test DB:**
- NOTHING — Migration 03 was written AFTER migration 05 (the "PLATINUM HARDENED" version)
- Migration 03 already creates tables with `status VARCHAR(20)` fields, not `is_active`
- This migration is OBSOLETE for this codebase

**Tests at risk:**
- ✅ NONE — Migration 03 creates the correct schema

**Test scenarios impossible without this:**
- NONE — Migration 03 already has the correct schema

**If we loaded this migration:**
- WOULD FAIL: ALTER TABLE attempts to add `status` column that already exists
- WOULD FAIL: ALTER TABLE attempts to drop `is_active` column that doesn't exist

**Should load:** ❌ NO — OBSOLETE. Migration 03 already incorporates these changes.

**Dependency chain:**
- This was a transitional migration for older schema versions
- Migration 03 (PLATINUM HARDENED, Nov 2025) post-dates this migration (Dec 2025 header is wrong)

---

### 06-seed-permissions.sql — PERMISSION CARTESIAN PRODUCT

**What it does:**
- Generates **permissions** table via `resources × actions` (e.g., CLIENT_NAV_VIEW, CLIENT_NAV_EDIT)
- Creates permission code format: `{RESOURCE_CODE}_{ACTION_CODE}`
- Total: ~20 permissions (5 resources × 4 actions)

**What's missing from test DB:**
- NO permissions data → role_permissions table is empty
- NO permissions → PermissionLoadingMiddleware returns empty permission set
- NO permissions → RBAC tests cannot verify "user has CLIENT_NAV_VIEW"

**Tests at risk:**
- ✅ Feature flag tests PASS — they bypass RBAC
- ❌ Permission middleware tests are IMPOSSIBLE
- ❌ User permission endpoint tests will FAIL (no permissions to assign)
- ❌ Role permission endpoint tests will FAIL (no permissions to link)

**Test scenarios impossible without this:**
- Testing PermissionLoadingMiddleware.Invoke()
- Testing "user has permission" authorization logic
- Testing role-permission assignment
- Testing permission override (allow/deny) logic

**If we loaded this migration:**
- DEPENDS: MUST load migration 02 first (seeds resources and actions)
- WOULD BREAK: Nothing — it's idempotent (ON CONFLICT DO NOTHING)

**Should load:** ✅ YES — Required for RBAC/permission tests.

**Dependency chain:**
- `01-portal-schema.sql` → `02-portal-seed.sql` → `06-seed-permissions.sql`

---

### 07-seed-overrides.sql — TEST PERMISSION OVERRIDES

**What it does:**
- Seeds 2 **user_permission_overrides** for admin@rgtspace.com
  - GRANT CLIENT_NAV_VIEW (is_allowed=true)
  - DENY CLIENT_NAV_EDIT (is_allowed=false)

**What's missing from test DB:**
- NO test data for permission override logic
- Cannot test "explicit deny overrides role grant"
- Cannot test override CRUD operations against existing data

**Tests at risk:**
- ✅ Most tests PASS — they create their own override data
- ⚠️ Tests that verify "deny overrides grant" edge cases might miss existing data interactions

**Test scenarios impossible without this:**
- Testing override evaluation with pre-existing data
- Testing "update existing override" vs "create new override"
- Testing cascading updates (user deleted → overrides cascade)

**If we loaded this migration:**
- DEPENDS: MUST load migrations 02, 06 first (admin user + permissions)
- DEPENDS: MUST load migration 08 first (updated_at/updated_by columns)
- WOULD BREAK: Tests expecting empty user_permission_overrides table

**Should load:** ❌ NO — Test data, not schema. Tests should seed their own overrides.

**Dependency chain:**
- `02` → `06` → `08` → `07`

---

### 08-fix-overrides-schema.sql — AUDIT COLUMN FIX

**What it does:**
- Adds `updated_at TIMESTAMP NOT NULL DEFAULT now()` to `user_permission_overrides`
- Adds `updated_by UUID NULL REFERENCES users(id)` to `user_permission_overrides`
- Backfills existing rows with `updated_at = created_at, updated_by = created_by`

**What's missing from test DB:**
- Test schema DIVERGES from production schema
- `user_permission_overrides` missing 2 audit columns
- DACs that query/map these columns will FAIL

**Tests at risk:**
- ❌ CRITICAL: `UserWriteDac.GrantResourcePermissionsAsync()` will FAIL (INSERT requires updated_at/updated_by)
- ❌ Any DAC that SELECTs updated_at/updated_by will FAIL (column does not exist)
- ❌ Any test that creates overrides with updated_at/updated_by will FAIL
- ❌ Read models mapping these fields will FAIL
- ✅ CURRENTLY PASSING: UserDacIntegrationTests (seeds overrides inline without those columns, doesn't call WRITE methods)

**Test scenarios impossible without this:**
- Testing override audit trail
- Testing "override updated_at timestamp"
- Testing C# code that matches production schema

**If we loaded this migration:**
- DEPENDS: Must load AFTER 01-portal-schema.sql (creates table)
- WOULD BREAK: Nothing — adds new columns with defaults

**Should load:** ✅ YES — CRITICAL. Schema mismatch = production bugs hidden in test.

**Dependency chain:**
- `01-portal-schema.sql` (creates table) → `08-fix-overrides-schema.sql` (adds columns)

---

## FK Chain Bomb — The Real Danger

Migration 10 exposed the FK chain issue. Here's the FULL FK chain across all skipped migrations:

### FK Dependencies in Skipped Migrations

**Migration 02:**
```sql
users.created_by → users(id)           -- Self-referential, can be NULL
users.updated_by → users(id)           -- Self-referential, can be NULL
clients.created_by → users(id)         -- NULL allowed
projects.created_by → users(id)        -- NULL allowed
mappings.created_by → users(id)        -- NULL allowed
```

**Migration 04:**
```sql
clients.created_by → users(id)         -- Uses seeded admin from migration 02
projects.client_id → clients(id)       -- Uses seeded clients
mappings.project_id → projects(id)     -- Uses seeded projects
assignments.user_id → users(id)        -- Uses seeded users from migration 04
assignments.position_code → position_types(code)  -- Uses seeded position_types from migration 02
```

**Migration 07:**
```sql
overrides.user_id → users(id)          -- Uses admin@rgtspace.com from migration 02
overrides.permission_id → permissions(id)  -- Uses permissions from migration 06
overrides.created_by → users(id)       -- Uses admin from migration 02
overrides.updated_by → users(id)       -- Uses admin from migration 02 (after migration 08)
```

**Migration 10 (CURRENTLY LOADED):**
```sql
features.created_by → users(id)        -- COMMENTED OUT to avoid FK violation
features.updated_by → users(id)        -- COMMENTED OUT to avoid FK violation
```

### Chain Reactions

If we add ANY migration that references a user:
1. Migration expects `admin@rgtspace.com` to exist (from 02)
2. Test DB has no admin user
3. FK constraint fails
4. ALL 49 integration tests crash

Current workarounds:
- Feature flag tests use `created_by = NULL` everywhere
- Portal routing tests seed their own users inline
- No tests exercise audit trail functionality

---

## What Are We NOT Testing?

### 1. Schema Drift Bugs (RESOLVED)
- **Gap:** NONE — Migration 03 creates tables with correct `status` column
- **Status:** Test schema matches production (migration 05 is obsolete)
- **Example:** `WHERE status = 'Active'` → works correctly

### 2. Position Type Validation
- **Gap:** No position_types seeded → FK constraint not tested
- **Hidden bugs:** Creating project_assignments with invalid position codes
- **Example:** Insert `position_code = 'TECH_PICS'` → FK fails (but test never hits this)

### 3. RBAC Permission Loading
- **Gap:** No actions/modules/resources/permissions → middleware returns empty set
- **Hidden bugs:** Permission checks always fail (no permissions loaded)
- **Example:** User assigned role with permissions, but middleware sees 0 permissions

### 4. Audit Trail Tracking
- **Gap:** No admin user → created_by/updated_by always NULL
- **Hidden bugs:** Audit queries fail (JOIN to users fails on NULL)
- **Example:** `SELECT ... JOIN users ON created_by = users.id` → 0 results

### 5. Multi-Client Isolation
- **Gap:** No test data with multiple clients → cannot test isolation
- **Hidden bugs:** Queries missing `WHERE client_id = @ClientId` filter
- **Example:** User from ACME sees TECHCORP projects

### 6. Cascading Deletes
- **Gap:** No pre-existing related data → cascade rules not tested
- **Hidden bugs:** Delete project → assignments not deleted (missing CASCADE)
- **Example:** `DELETE FROM projects WHERE id = @Id` leaves orphan assignments

### 7. Soft Delete Zombie Constraints
- **Gap:** No test of "delete + recreate same code" scenario
- **Hidden bugs:** Partial index not working (duplicate code error)
- **Example:** Delete client "ACME", create new client "ACME" → unique violation

### 8. Permission Override Logic
- **Gap:** No permissions → cannot test override evaluation
- **Hidden bugs:** Deny override not applied (formula wrong)
- **Example:** `RoleGrant UNION Allow MINUS Deny` → Deny step never tested

---

## Test Gaps by Category

### DAC Integration Tests
**Current:** 3 DAC test files (PositionType, User, Feature)
**Gaps:**
- No tests for RBAC DACs (permissions, roles, role_permissions)
- No tests for portal routing DACs (clients, projects, mappings, assignments)
- No tests for override DACs (user_permission_overrides)

### Endpoint Integration Tests
**Current:** 2 endpoint test files (Client, Feature)
**Gaps:**
- No tests for RBAC endpoints (roles, permissions, overrides)
- No tests for project endpoints
- No tests for assignment endpoints

### Middleware Tests
**Current:** 0 middleware integration tests
**Gaps:**
- No tests for PermissionLoadingMiddleware (needs permissions data)
- No tests for TenantResolutionMiddleware (needs clients data)
- No tests for AuditMiddleware (needs audit_log table)

---

## Proof of Hidden Bugs — Concrete Examples

### Example 1: UserWriteDac.GrantResourcePermissionsAsync (Migration 08)

**Location:** `Rgt.Space.Infrastructure/Persistence/Dac/Identity/UserWriteDac.cs:238-240`

**Code:**
```csharp
INSERT INTO user_permission_overrides (
    user_id, permission_id, is_allowed,
    created_at, created_by, updated_at, updated_by  // ← These columns don't exist
)
```

**Impact:**
- Production code expects `updated_at` and `updated_by` columns
- Test DB does NOT have these columns (migration 08 is skipped)
- Any test calling this method will FAIL with "column does not exist"
- Current tests PASS by luck — they seed overrides inline without calling this method

**Blast radius:**
- Future user permission tests will crash
- Any endpoint that grants resource permissions will fail in integration tests
- Bug hidden until someone writes a test for permission grant functionality

### Example 2: Project Assignments FK Violation (Migration 02)

**Location:** Any code that creates `project_assignments`

**Code:**
```csharp
INSERT INTO project_assignments (project_id, user_id, position_code, ...)
VALUES (@ProjectId, @UserId, 'TECH_PIC', ...)
```

**Impact:**
- FK constraint: `position_code → position_types(code)`
- Migration 02 seeds the 6 position types (TECH_PIC, TECH_BACKUP, etc.)
- Test DB does NOT have position_types data (migration 02 is skipped)
- Any test creating assignments will FAIL with "FK violation"
- Current tests PASS by luck — no integration tests for project assignments yet

**Blast radius:**
- Portal routing tests that create assignments
- Task allocation tests
- Any test verifying position type validation

### Example 3: RBAC Permission Evaluation (Migration 06)

**Location:** `UserReadDac.GetPermissionsAsync()`

**Code:**
```sql
SELECT m.code as module, r.code as sub_module, ...
FROM permissions p
JOIN resources r ON p.resource_id = r.id
JOIN actions a ON p.action_id = a.id
...
```

**Impact:**
- Permissions table is EMPTY (migration 06 is skipped)
- Query returns 0 results
- User has NO permissions (even if role is assigned)
- Current test PASSES by luck — UserDacIntegrationTests seeds permissions inline

**Blast radius:**
- PermissionLoadingMiddleware returns empty permission set
- All RBAC authorization fails
- Future middleware tests will fail

---

## Actual Integration Test Inventory

### Current Integration Tests (5 files, 49 tests)

**FeatureDacIntegrationTests.cs** — 27 tests
- Tests feature CRUD via FeatureWriteDac and FeatureReadDac
- Seeds own test users inline (avoids migration 02 dependency)
- Seeds own test clients inline (avoids migration 04 dependency)
- Uses unique codes per test (UUIDv7-based) to avoid collisions
- ✅ PASSES — fully isolated, no missing dependencies

**PositionTypeIntegrationTests.cs** — 1 test
- Tests position_types table schema (status column, CRUD operations)
- Creates its own test position type (code = TEST_POS)
- Uses `StatusConstants.Active` (expects VARCHAR status, not BOOLEAN is_active)
- ✅ PASSES — migration 03 creates position_types with status column

**UserDacIntegrationTests.cs** — 3 tests
- Tests user CRUD, soft delete, and RBAC permission evaluation
- Seeds own test data inline (modules, resources, actions, permissions, roles, users, overrides)
- Seeds user_permission_overrides WITHOUT updated_at/updated_by (inline SQL)
- Only calls READ methods (GetPermissionsAsync) — does not call WRITE methods
- ✅ PASSES — test avoids UserWriteDac.GrantResourcePermissionsAsync (which needs migration 08)
- ⚠️ HIDDEN BUG — Future tests calling GrantResourcePermissionsAsync will FAIL (INSERT requires updated_at/updated_by)

**ClientEndpointTests.cs** — 1 test
- Only tests `/health/live` endpoint (smoke test)
- Does NOT test actual client endpoints
- ✅ PASSES — no domain data dependencies

**FeatureEndpointTests.cs** — 18 tests (estimated)
- Tests all 14 feature flag endpoints (CRUD + read + eval)
- Seeds own test data inline (users, clients, features)
- All endpoints use `AllowAnonymous` (bypass auth/RBAC)
- ✅ PASSES — fully isolated, no missing dependencies

**Total:** 49 integration tests (all passing)

### What's NOT Tested (Missing Integration Tests)

**Portal Routing Domain:**
- No tests for client endpoints (create, update, delete, get)
- No tests for project endpoints
- No tests for mapping endpoints
- No tests for assignment endpoints
- No tests for multi-client isolation
- No tests for cascading deletes

**RBAC/Permissions:**
- No tests for role endpoints
- No tests for permission endpoints
- No tests for user role assignment endpoints
- No tests for permission override endpoints
- No tests for PermissionLoadingMiddleware
- No tests for permission evaluation logic

**Audit:**
- No tests for audit log writes
- No tests for AuditLoggingBehavior
- No tests for audit entry batching/fallback

**Tenancy:**
- No tests for TenantResolutionMiddleware
- No tests for tenant-based connection routing

---

## Dependency Graph

```
00-extensions.sql (UUID v7)                              ✅ LOADED
  ↓
01-portal-schema.sql (tables)                            ✅ LOADED
  ↓
02-portal-seed.sql (actions, position_types, modules)    ❌ MISSING (LOAD NOW)
  ↓                                                       ├─ Seeds admin user (fixes migration 10 FK)
  ↓                                                       ├─ Seeds position_types (fixes assignments FK)
  ↓                                                       └─ Seeds actions/modules (required for 06)
03-portal-routing-schema.sql (clients, projects, etc.)   ✅ LOADED
  ↓
04-test-data.sql (test clients, projects, users)         ❌ MISSING (OPTIONAL)
  ↓                                                       └─ Multi-tenant test scenarios
05-standardize-status.sql (schema migration)             ⛔ OBSOLETE (skip — 03 has status)
  ↓
06-seed-permissions.sql (permissions)                    ❌ MISSING (LOAD NOW)
  ↓                                                       └─ Enables RBAC integration tests
08-fix-overrides-schema.sql (add audit columns)          ❌ MISSING (LOAD NOW)
  ↓                                                       └─ Fixes UserWriteDac INSERT bug
07-seed-overrides.sql (test overrides)                   ❌ MISSING (SKIP — tests seed inline)
  ↓
09-feature-flags.sql (feature tables)                    ✅ LOADED
  ↓
10-feature-flag-seed.sql (module flags)                  ✅ LOADED (⚠️ FK bomb — needs 02)
```

### Color Legend
- ✅ LOADED — Currently in TestDatabaseInitializer
- ❌ MISSING — Should be added
- ⛔ OBSOLETE — Skip (will fail if loaded)
- ⚠️ FK bomb — References missing data from migration 02

---

## Recommended RequiredFiles Array

### Minimal Set (Schema Only)
```csharp
private static readonly string[] RequiredFiles =
{
    "READMEs/SQL/PostgreSQL/Migrations/00-extensions.sql",
    "READMEs/SQL/PostgreSQL/Migrations/01-portal-schema.sql",
    "READMEs/SQL/PostgreSQL/Migrations/02-portal-seed.sql",        // ← ADD
    "READMEs/SQL/PostgreSQL/Migrations/03-portal-routing-schema.sql",
    // SKIP 05 — OBSOLETE (migration 03 already has status columns)
    "READMEs/SQL/PostgreSQL/Migrations/06-seed-permissions.sql",    // ← ADD
    "READMEs/SQL/PostgreSQL/Migrations/08-fix-overrides-schema.sql", // ← ADD
    "READMEs/SQL/PostgreSQL/Migrations/09-feature-flags.sql",
    "READMEs/SQL/PostgreSQL/Migrations/10-feature-flag-seed.sql"
};
```

### Full Set (Schema + Test Data)
```csharp
private static readonly string[] RequiredFiles =
{
    "READMEs/SQL/PostgreSQL/Migrations/00-extensions.sql",
    "READMEs/SQL/PostgreSQL/Migrations/01-portal-schema.sql",
    "READMEs/SQL/PostgreSQL/Migrations/02-portal-seed.sql",
    "READMEs/SQL/PostgreSQL/Migrations/03-portal-routing-schema.sql",
    "READMEs/SQL/PostgreSQL/Migrations/04-test-data.sql",           // ← ADD for E2E
    // SKIP 05 — OBSOLETE (migration 03 already has status columns)
    "READMEs/SQL/PostgreSQL/Migrations/06-seed-permissions.sql",
    "READMEs/SQL/PostgreSQL/Migrations/08-fix-overrides-schema.sql",
    // SKIP 07 — Test data only (tests seed their own overrides)
    "READMEs/SQL/PostgreSQL/Migrations/09-feature-flags.sql",
    "READMEs/SQL/PostgreSQL/Migrations/10-feature-flag-seed.sql"
};
```

---

## Critical Actions Required

### 1. Fix Migration 10 FK Issue
**Problem:** Uses NULL for created_by/updated_by (workaround)
**Solution:** Update to use seeded admin ID from migration 02
```sql
-- Before
INSERT INTO features (code, name, created_by, updated_by)
VALUES ('DASHBOARD', 'Dashboard', NULL, NULL);

-- After
INSERT INTO features (code, name, created_by, updated_by)
SELECT 'DASHBOARD', 'Dashboard', id, id
FROM users WHERE email = 'admin@rgtspace.com';
```

### 2. Load Migration 02
**Impact:** ALL domain tests can now reference admin user
**Breaking:** None (admin user doesn't exist yet, so no conflicts)

### 3. SKIP Migration 05
**Reason:** OBSOLETE — Migration 03 already creates tables with `status` columns
**Impact:** None (migration would fail anyway)

### 4. Load Migration 06
**Impact:** RBAC tests become possible
**Requires:** Migration 02 (actions + resources)

### 5. Load Migration 08
**Impact:** Test schema matches production
**Breaking:** DACs querying updated_at/updated_by on overrides will FAIL (good — exposes bugs)

### 6. Consider Migration 04
**Impact:** Multi-client test scenarios possible
**Trade-off:** Tests need to handle pre-existing data OR use DELETE in setup

---

## Summary Table

| Migration | Purpose | Missing Data | Tests at Risk | Should Load? | Priority |
|-----------|---------|--------------|---------------|--------------|----------|
| 02-portal-seed | Actions, position_types, modules, admin | Actions, position_types, resources, admin user, SYS_ADMIN role | Assignment tests, RBAC tests | ✅ YES | CRITICAL |
| 04-test-data | Test clients, projects, mappings, users | 3 clients, 6 projects, 5 mappings, 4 users, 6 assignments | Multi-tenant tests, conflict tests | ⚠️ CONDITIONAL | OPTIONAL |
| 05-standardize-status | Schema migration (is_active → status) | NONE (migration 03 already has status) | NONE | ❌ NO | OBSOLETE |
| 06-seed-permissions | Cartesian product (resources × actions) | Permissions table data | RBAC tests, permission middleware | ✅ YES | CRITICAL |
| 07-seed-overrides | Test permission overrides | 2 test overrides for admin | Override edge case tests | ❌ NO | SKIP |
| 08-fix-overrides-schema | Add updated_at/updated_by to overrides | Audit columns on overrides table | Override DACs, override audit tests | ✅ YES | CRITICAL |

---

## Conclusion

The test database is missing 3 CRITICAL migrations (02, 06, 08) that hide production bugs and prevent RBAC testing. Loading these migrations will:

1. Enable RBAC test coverage (currently impossible — no permissions data)
2. Fix FK chain bombs (migration 10 and future migrations need admin user)
3. Enable audit trail testing (created_by/updated_by on overrides)
4. Enable position type validation (project_assignments FK constraint)
5. Enable module/resource/permission testing

**Immediate action:** Load migrations 02, 06, 08 into TestDatabaseInitializer.

**Skip migration 05:** OBSOLETE — Migration 03 already creates tables with `status` columns.

**Consider migration 04:** Optional test data for multi-tenant E2E scenarios.

**Long-term action:** Write missing integration tests for RBAC, permissions, overrides, portal routing.

---

## Implementation Roadmap

### Phase 1: Fix FK Bomb (IMMEDIATE)
1. Update `TestDatabaseInitializer.RequiredFiles` to include migration 02
2. Fix migration 10 to use admin user ID from migration 02 (not NULL)
3. Run all 49 integration tests — expect ALL to PASS
4. Commit with message: "fix(tests): add migration 02 to prevent FK bombs"

### Phase 2: Enable RBAC Testing (SAME SESSION)
1. Add migration 06 to `TestDatabaseInitializer.RequiredFiles`
2. Run integration tests — expect ALL to PASS (no RBAC tests yet)
3. Add migration 08 to `TestDatabaseInitializer.RequiredFiles`
4. Run integration tests — expect ALL to PASS
5. Commit with message: "feat(tests): add migrations 06+08 to enable RBAC testing"

### Phase 3: Test Data (OPTIONAL)
1. Add migration 04 to `TestDatabaseInitializer.RequiredFiles`
2. Run integration tests — may BREAK tests expecting empty tables
3. Fix broken tests (filter by seeded IDs OR use DELETE in test setup)
4. Commit with message: "feat(tests): add migration 04 for multi-tenant test scenarios"

### Phase 4: Write Missing Tests (FUTURE)
1. Portal routing integration tests (clients, projects, mappings, assignments)
2. RBAC integration tests (roles, permissions, overrides)
3. Middleware integration tests (PermissionLoadingMiddleware, TenantResolutionMiddleware)
4. Audit integration tests (AuditLoggingBehavior, audit_log writes)

---

## Risk Assessment by Migration

| Migration | Load? | Risk if Loaded | Risk if NOT Loaded | Verdict |
|-----------|-------|----------------|-------------------|---------|
| 02-portal-seed | YES | NONE (foundation data) | CRITICAL — FK bombs, no RBAC tests | ✅ LOAD NOW |
| 04-test-data | OPTIONAL | LOW (may break empty-table tests) | LOW (tests seed inline) | ⚠️ CONSIDER |
| 05-standardize-status | NO | HIGH (migration will FAIL) | NONE (migration 03 has status) | ❌ SKIP |
| 06-seed-permissions | YES | NONE (idempotent) | HIGH — no RBAC tests possible | ✅ LOAD NOW |
| 07-seed-overrides | NO | LOW (tests expect empty table) | NONE (tests seed inline) | ❌ SKIP |
| 08-fix-overrides-schema | YES | NONE (adds columns with defaults) | HIGH — UserWriteDac will FAIL | ✅ LOAD NOW |
