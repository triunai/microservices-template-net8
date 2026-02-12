# SQL Schema Code Review

**Reviewer:** Claude Opus 4.6 (Automated Code Review Agent)
**Date:** 2026-02-10
**Scope:** All SQL files in `READMEs/SQL/PostgreSQL/` (11 migrations + 22 table definitions)
**Context:** .NET 8 API using Dapper (no EF Core), PostgreSQL, soft deletes, UUIDv7

---

## Files Reviewed

### Migrations (Authoritative -- these run in production/test)
| File | Purpose |
|------|---------|
| `Migrations/00-extensions.sql` | pgcrypto + UUIDv7 polyfill |
| `Migrations/01-portal-schema.sql` | Core IAM tables (users, sessions, RBAC, audit_log) |
| `Migrations/02-portal-seed.sql` | Seed: actions, position_types, modules, resources, admin user |
| `Migrations/03-portal-routing-schema.sql` | Portal Routing + Task Allocation tables |
| `Migrations/04-test-data.sql` | Dev/test seed data (clients, projects, mappings, assignments) |
| `Migrations/05-standardize-status.sql` | Migration: is_active -> VARCHAR status |
| `Migrations/06-seed-permissions.sql` | Cross-join permission generation |
| `Migrations/07-seed-overrides.sql` | Seed permission overrides for admin |
| `Migrations/08-fix-overrides-schema.sql` | Add missing audit columns to overrides |
| `Migrations/09-feature-flags.sql` | Feature flag tables + initial seed |
| `Migrations/10-feature-flag-seed.sql` | Module feature flag seeds |

### Table Definitions (Reference docs -- NOT executed)
22 files in `Tables/` directory. These serve as documentation and may drift from the authoritative migration files.

### Test Initialization
`TestDatabaseInitializer.cs` loads: 00, 01, 03, 09, 10 (skips 02, 04, 05, 06, 07, 08)

---

## Summary of Findings

| Severity | Count | Category |
|----------|-------|----------|
| CRITICAL | 3 | FK anti-patterns, seed idempotency, migration schema conflict |
| HIGH | 8 | Missing indexes, missing triggers, missing soft deletes, ON CONFLICT issues |
| MEDIUM | 9 | Inconsistencies, constraint gaps, timestamp drift, documentation drift |
| LOW | 5 | Naming, style, minor optimizations |
| INFO | 4 | Architecture observations |

---

## CRITICAL Findings

### CRITICAL-01: Audit Columns FK-Constrained to users(id) -- The Root Cause of Test Failures

**Severity:** CRITICAL
**Impact:** Blocks all test seeding; blocks user deletion; creates circular dependency
**Files affected:** Every table in `01-portal-schema.sql` and `03-portal-routing-schema.sql`

The `created_by`, `updated_by`, and `deleted_by` columns across **13 tables** have foreign key constraints referencing `users(id)`. This is the known anti-pattern that caused integration test failures.

**Full inventory of affected tables:**

| Table | created_by FK | updated_by FK | deleted_by FK |
|-------|:---:|:---:|:---:|
| `users` (self-ref) | YES | YES | YES |
| `modules` | YES | YES | YES |
| `resources` | YES | YES | YES |
| `actions` | YES | YES | -- |
| `permissions` | YES | YES | -- |
| `roles` | YES | YES | -- |
| `user_roles` | YES (assigned_by_user_id) | -- | -- |
| `user_permission_overrides` | YES | -- | -- |
| `clients` | YES | YES | YES |
| `projects` | YES | YES | YES |
| `client_project_mappings` | YES | YES | YES |
| `project_assignments` | YES | YES | YES |
| `features` | YES | YES | YES |
| `client_features` | YES | YES | YES |
| `user_feature_overrides` | YES (created_by) | -- | -- |

**Total: 13 tables with 34 FK constraints on audit columns pointing to users(id)**

**Problems this causes:**
1. **Seed ordering nightmare:** Cannot insert into `modules`, `resources`, `clients`, etc. with a `created_by` value unless that user already exists in `users`. But the `users` table itself has a self-referential FK on `created_by`.
2. **Circular dependency on users:** `users.created_by REFERENCES users(id)` -- the first user ever inserted cannot have a `created_by` unless it is NULL.
3. **User deletion permanently blocked:** Even with soft deletes, if you ever need to hard-delete a user, you would need to NULL out `created_by`/`updated_by` across every table in the entire database first.
4. **Test isolation broken:** Tests that seed data into any table must first ensure the referenced user ID exists, creating fragile test setup chains.

**Current workaround (visible in migrations):** All audit FK columns are `NULL`, so seed scripts leave `created_by`/`updated_by` as NULL. This works but defeats the purpose of having the FK in the first place.

**Recommended fix:**
```sql
-- REMOVE FK constraints from all audit columns. Audit columns should be
-- informational, not referentially enforced. The application layer (C# handlers)
-- already validates user existence before writing.
-- Keep the columns, drop the REFERENCES clause.

-- Example for clients:
-- BEFORE: created_by UUID NULL REFERENCES users(id)
-- AFTER:  created_by UUID NULL
```

---

### CRITICAL-02: Migration 02 Seed Script Uses Module INSERTs Without ON CONFLICT Guards for Resources

**Severity:** CRITICAL
**Impact:** Migration 02 will fail on re-run; non-idempotent
**File:** `Migrations/02-portal-seed.sql`, lines 27-65

The module/resource seeding in migration 02 uses `DO $$ ... INSERT INTO modules ... RETURNING id INTO v_mod_id ... INSERT INTO resources ...` blocks. These have **no idempotency protection**:

```sql
-- Line 31-37: No ON CONFLICT protection
INSERT INTO modules (id, name, code, sort_order)
VALUES (uuid_generate_v7(), 'Portal Routing', 'PORTAL_ROUTING', 1)
RETURNING id INTO v_mod_id;

INSERT INTO resources (id, module_id, name, code) VALUES
    (uuid_generate_v7(), v_mod_id, 'Client Navigation', 'CLIENT_NAV'),
    (uuid_generate_v7(), v_mod_id, 'Admin Routing', 'ADMIN_ROUTING');
```

**Problem:** If `modules_code_uk` constraint fires (module already exists), the `RETURNING id INTO v_mod_id` fails, and `v_mod_id` is NULL. The subsequent `INSERT INTO resources` with a NULL `module_id` will then violate the `NOT NULL` constraint on `resources.module_id`.

**This is not caught by TestDatabaseInitializer** because migration 02 is not in the `RequiredFiles` list.

**Recommended fix:** Use `INSERT ... ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name RETURNING id INTO v_mod_id` or use a separate `SELECT id INTO v_mod_id FROM modules WHERE code = 'PORTAL_ROUTING'` with a fallback INSERT.

---

### CRITICAL-03: Migration 05 (Standardize Status) Conflicts with Migration 03 Schema

**Severity:** CRITICAL
**Impact:** Migration 05 assumes columns exist that migration 03 already defines differently
**Files:** `Migrations/05-standardize-status.sql` vs `Migrations/03-portal-routing-schema.sql`

Migration 03 creates `client_project_mappings` with a `status VARCHAR(20)` column (line 149) and a `CHECK (status IN ('Active', 'Inactive'))` constraint.

Migration 05 then tries to:
1. `ALTER TABLE client_project_mappings ADD COLUMN status VARCHAR(20)` -- **will fail** because `status` already exists (added by migration 03)
2. `ALTER TABLE client_project_mappings DROP COLUMN is_active` -- **will fail** because `is_active` does not exist (migration 03 never created it)

**Root cause:** Migration 03 was written AFTER migration 05 was designed, or migration 03 incorporated the fix from 05 directly. The two migrations now conflict.

Similarly for `position_types`: Migration 03 creates it with `status VARCHAR(20)` (line 197), but migration 05 tries to add a `status` column (assuming `is_active` exists).

**Impact on TestDatabaseInitializer:** This is masked because the test initializer skips migrations 02, 04, 05, 06, 07, 08. But running the full migration chain in production would fail at step 05.

**Recommended fix:** Migration 05 should be marked as superseded/skipped, or wrapped in a conditional check:
```sql
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_name = 'client_project_mappings' AND column_name = 'is_active') THEN
        -- Run migration
    ELSE
        RAISE NOTICE 'Migration 05 already applied via schema definition. Skipping.';
    END IF;
END $$;
```

---

## HIGH Findings

### HIGH-01: Missing FK Indexes on Multiple Tables

**Severity:** HIGH
**Impact:** Full table scans on JOIN operations; degraded query performance
**Files:** `Migrations/01-portal-schema.sql`, `Migrations/03-portal-routing-schema.sql`, `Migrations/09-feature-flags.sql`

PostgreSQL does NOT automatically create indexes on foreign key columns. The following FK columns are missing covering indexes:

| Table | Column | FK Target | Index Exists? |
|-------|--------|-----------|:---:|
| `resources` | `module_id` | `modules(id)` | NO |
| `permissions` | `resource_id` | `resources(id)` | NO |
| `permissions` | `action_id` | `actions(id)` | NO |
| `user_roles` | `role_id` | `roles(id)` | NO |
| `user_permission_overrides` | `permission_id` | `permissions(id)` | NO |
| `user_sessions` | `user_id` | `users(id)` | NO (in migration -- yes in UAM-Schema) |
| `user_feature_overrides` | `feature_id` | `features(id)` | NO |

**Why this matters:** Any query that JOINs on these columns (e.g., "get all permissions for a resource") will trigger a sequential scan. With Dapper raw SQL, these JOINs are very common.

The `role_permissions` table has `idx_role_permissions_role` on `role_id` but is missing an index on `permission_id`. The `audit_log.sql` table definition file adds `idx_role_permissions_role` but the migration 01 already has it.

**Recommended fix:** Add indexes for all FK columns used in JOINs:
```sql
CREATE INDEX idx_resources_module ON resources(module_id);
CREATE INDEX idx_permissions_resource ON permissions(resource_id);
CREATE INDEX idx_permissions_action ON permissions(action_id);
CREATE INDEX idx_user_perm_overrides_perm ON user_permission_overrides(permission_id);
CREATE INDEX idx_user_feature_overrides_feature ON user_feature_overrides(feature_id);
```

---

### HIGH-02: Missing update_updated_at Triggers for IAM Tables

**Severity:** HIGH
**Impact:** `updated_at` column will be stale unless application code explicitly sets it
**Files:** `Migrations/01-portal-schema.sql`, `Migrations/03-portal-routing-schema.sql`

Migration 03 creates the `update_updated_at_column()` trigger function and applies it to 5 tables: `clients`, `projects`, `client_project_mappings`, `position_types`, `project_assignments`.

However, the following tables have `updated_at` columns but **NO trigger**:

| Table | Has updated_at? | Has Trigger? |
|-------|:---:|:---:|
| `users` | YES | NO |
| `modules` | YES | NO |
| `resources` | YES | NO |
| `actions` | YES | NO |
| `permissions` | YES | NO |
| `roles` | YES | NO |
| `user_permission_overrides` | YES (added in 08) | NO |
| `features` | YES | NO |
| `client_features` | YES | NO |

**9 tables are missing the auto-update trigger.**

The trigger function is created in migration 03, but migration 01 (which creates the IAM tables) runs BEFORE migration 03. No migration adds triggers to the IAM tables retroactively.

Migration 09 (feature flags) also does not create triggers for `features` or `client_features`.

**Recommended fix:** Add a new migration that applies the trigger to all tables with `updated_at`:
```sql
CREATE TRIGGER update_users_timestamp BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_modules_timestamp BEFORE UPDATE ON modules
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
-- ... etc for all 9 tables
```

---

### HIGH-03: actions and roles Tables Missing Soft Delete Columns

**Severity:** HIGH
**Impact:** Cannot soft-delete actions or roles; inconsistent with other tables
**Files:** `Migrations/01-portal-schema.sql` lines 104-115 (actions), lines 135-150 (roles)

The `actions` table has `created_at`, `created_by`, `updated_at`, `updated_by` but is **missing**:
- `is_deleted BOOLEAN NOT NULL DEFAULT FALSE`
- `deleted_at TIMESTAMP NULL`
- `deleted_by UUID NULL`

The `roles` table has the same gap.

Every other entity table in the schema has soft delete support. The `permissions` table is also missing soft delete columns.

**Consequence:** If the application tries to soft-delete an action, role, or permission, the Dapper query will fail with "column is_deleted does not exist". Hard deletes are required, which conflicts with the codebase convention.

The `users` table does have soft delete but uses a regular `UNIQUE` constraint on `email` instead of a partial unique index -- meaning a soft-deleted user's email cannot be reused.

---

### HIGH-04: users Table email Unique Constraint Not Soft-Delete-Aware

**Severity:** HIGH
**Impact:** Cannot re-create a user with the same email after soft-deleting
**File:** `Migrations/01-portal-schema.sql`, line 45

```sql
CONSTRAINT users_email_uk UNIQUE (email)
```

This is a full unique constraint, not a partial index. If a user with email `john@example.com` is soft-deleted (`is_deleted = TRUE`), you CANNOT create a new user with the same email. This violates the "zombie constraint" pattern used consistently elsewhere (clients, projects, mappings, features, client_features all use `WHERE is_deleted = FALSE`).

Similarly, `users_sso_uk UNIQUE (sso_provider, external_id)` is a full constraint and would prevent re-linking an SSO identity after soft-delete.

**Recommended fix:**
```sql
-- Drop the full constraint
ALTER TABLE users DROP CONSTRAINT users_email_uk;
-- Add partial unique index
CREATE UNIQUE INDEX idx_users_email_active ON users(email) WHERE is_deleted = FALSE;

ALTER TABLE users DROP CONSTRAINT users_sso_uk;
CREATE UNIQUE INDEX idx_users_sso_active ON users(sso_provider, external_id) WHERE is_deleted = FALSE;
```

---

### HIGH-05: modules_code_uk and Similar Constraints Not Soft-Delete-Aware

**Severity:** HIGH
**Impact:** Cannot reuse module/resource/action/role/permission codes after soft-delete
**File:** `Migrations/01-portal-schema.sql`

The following unique constraints are regular (not partial), meaning soft-deleted records still occupy the unique namespace:

| Table | Constraint | Soft-Delete Aware? |
|-------|-----------|:---:|
| `modules` | `modules_code_uk UNIQUE (code)` | NO |
| `resources` | `resources_module_code_uk UNIQUE (module_id, code)` | NO |
| `actions` | `actions_code_uk UNIQUE (code)` | NO |
| `permissions` | `permissions_resource_action_uk UNIQUE (resource_id, action_id)` | NO |
| `permissions` | `permissions_code_uk UNIQUE (code)` | NO |
| `roles` | `roles_code_uk UNIQUE (code)` | NO |
| `user_roles` | `user_roles_uk UNIQUE (user_id, role_id)` | NO |
| `user_permission_overrides` | `user_permission_overrides_uk UNIQUE (user_id, permission_id)` | NO |

Compare with the Portal Routing domain tables which correctly use partial indexes:
- `idx_clients_code_active ON clients(code) WHERE is_deleted = FALSE` -- CORRECT
- `idx_features_code_active ON features(code) WHERE is_deleted = FALSE` -- CORRECT

**This means the IAM domain (migration 01) was built BEFORE the zombie constraint pattern was established (migration 03), and was never retrofitted.**

---

### HIGH-06: Migration 10 Uses ON CONFLICT (id) Instead of ON CONFLICT (code)

**Severity:** HIGH
**Impact:** Seed is not truly idempotent -- same feature code can be inserted with different IDs
**File:** `Migrations/10-feature-flag-seed.sql`, line 12

```sql
ON CONFLICT (id) DO NOTHING;
```

The seed uses hardcoded UUIDs for the `id` column. The conflict target is `(id)` (the PK). If someone manually inserts a feature with code `DASHBOARD` but a different UUID, this seed will insert a SECOND row with code `DASHBOARD` and the hardcoded UUID, violating the business constraint (which is only a partial unique index on `code WHERE is_deleted = FALSE`).

The correct approach is to conflict on the business key:
```sql
-- Cannot use ON CONFLICT (code) because it's a partial unique index.
-- Must use the WHERE clause form or NOT EXISTS guard.
INSERT INTO features (id, code, name, description, is_active, requires_client)
SELECT '019c4700-0001-7000-0000-000000000001', 'DASHBOARD', ...
WHERE NOT EXISTS (SELECT 1 FROM features WHERE code = 'DASHBOARD' AND is_deleted = FALSE);
```

---

### HIGH-07: Migration 09 Feature Flag Seeds Have No Idempotency Protection

**Severity:** HIGH
**Impact:** Re-running migration 09 will fail with duplicate code violation
**File:** `Migrations/09-feature-flags.sql`, lines 98-102

```sql
INSERT INTO features (code, name, description, is_active, requires_client)
VALUES ('DASHBOARD_V2', 'Dashboard V2', 'New dashboard experience', FALSE, TRUE);

INSERT INTO features (code, name, description, is_active, requires_client)
VALUES ('SYS_COMBO_BREAK_DEBUG', 'Combo Break Debugger', ...);
```

No `ON CONFLICT` clause. No `WHERE NOT EXISTS` guard. If this migration runs twice, it will fail on `idx_features_code_active` (the partial unique index on `code WHERE is_deleted = FALSE`).

**Recommended fix:** Add idempotency guards:
```sql
INSERT INTO features (code, name, description, is_active, requires_client)
VALUES ('DASHBOARD_V2', ...)
ON CONFLICT (id) DO NOTHING;  -- But no explicit id, so use WHERE NOT EXISTS instead
```

Actually, since these INSERTs use `DEFAULT uuid_generate_v7()` for the `id`, `ON CONFLICT (id)` would never match. Must use the NOT EXISTS pattern:
```sql
INSERT INTO features (code, name, description, is_active, requires_client)
SELECT 'DASHBOARD_V2', 'Dashboard V2', 'New dashboard experience', FALSE, TRUE
WHERE NOT EXISTS (SELECT 1 FROM features WHERE code = 'DASHBOARD_V2' AND is_deleted = FALSE);
```

---

### HIGH-08: user_permission_overrides Missing updated_at/updated_by in Original Schema

**Severity:** HIGH (Fixed in migration 08 but creates ordering dependency)
**Impact:** Migration 07 references columns that don't exist until migration 08
**Files:** `Migrations/01-portal-schema.sql` lines 168-179, `Migrations/07-seed-overrides.sql`, `Migrations/08-fix-overrides-schema.sql`

The `user_permission_overrides` table in migration 01 does NOT have `updated_at` or `updated_by` columns. Migration 07 (seed overrides) inserts data with `updated_by` column:

```sql
-- Migration 07, line 28:
INSERT INTO user_permission_overrides (
    user_id, permission_id, is_allowed, reason, created_by, updated_by  -- updated_by doesn't exist yet!
) VALUES (...)
```

But `updated_by` is not added until migration 08. This means **migration 07 will fail** if run before migration 08.

The migrations are numbered 07 then 08, suggesting the intent is sequential, but the TestDatabaseInitializer skips both, so this ordering bug is never caught in tests.

---

## MEDIUM Findings

### MEDIUM-01: Inconsistent Timestamp Defaults Between IAM and Portal Routing Domains

**Severity:** MEDIUM
**Impact:** Subtle timezone differences in stored data

| Domain | Default Expression | Timezone Handling |
|--------|-------------------|-------------------|
| IAM (migration 01) | `DEFAULT now()` | Depends on server `timezone` setting |
| Portal Routing (migration 03) | `DEFAULT (now() AT TIME ZONE 'utc')` | Explicit UTC |
| Feature Flags (migration 09) | `DEFAULT (now() AT TIME ZONE 'utc')` | Explicit UTC |

The IAM tables use bare `now()` which returns the server's local time (which per `UAM-Schema.sql` comments, should be `Asia/Kuala_Lumpur`). The Portal Routing and Feature Flag tables explicitly convert to UTC.

**Combined with `TIMESTAMP WITHOUT TIME ZONE`:** The IAM tables will store KL timestamps as if they were UTC. The Portal Routing tables will store actual UTC. If a query joins across domains, the timestamps are 8 hours apart.

The `audit_log` table is the only one using `TIMESTAMPTZ` (timezone-aware), adding a third inconsistency.

---

### MEDIUM-02: Table Definition Files (Tables/) Drift from Authoritative Migrations

**Severity:** MEDIUM
**Impact:** Developers reading the wrong files get incorrect schema information

Key drifts observed:

1. **`Tables/user_permission_overrides.sql`** -- Missing `updated_at`/`updated_by` (not updated after migration 08 fix)
2. **`Tables/modules.sql`** -- Uses `is_active BOOLEAN` but production schema may have migrated to `status VARCHAR`
3. **`Tables/UAM-Schema.sql`** -- Contains a completely different architecture with `tenants` table and tenant-scoped RBAC, plus `groups`, `group_members`, `group_roles` tables that do not exist in the production schema
4. **`Tables/UAM-Tenantless.sql`** -- Not SQL but a markdown discussion about schema decisions (misnamed file)
5. **`Tables/actions copy.sql`** -- A duplicate of `actions.sql` (leftover copy-paste)
6. **`Tables/audit_log.sql`** -- Contains index definitions for tables OTHER than audit_log (e.g., `idx_users_email`, `idx_proj_assignments_proj`) that belong in separate files

---

### MEDIUM-03: ON DELETE Behavior Inconsistencies Across Domain Boundaries

**Severity:** MEDIUM
**Impact:** Confusing deletion semantics; some deletions cascade, others block silently

| FK Relationship | ON DELETE Behavior | Notes |
|----------------|-------------------|-------|
| `user_sessions.user_id -> users(id)` | CASCADE | Session dies with user |
| `user_roles.user_id -> users(id)` | (none = RESTRICT) | User deletion blocked |
| `user_roles.role_id -> roles(id)` | (none = RESTRICT) | Role deletion blocked |
| `user_permission_overrides.user_id -> users(id)` | (none = RESTRICT) | User deletion blocked |
| `projects.client_id -> clients(id)` | RESTRICT (explicit) | Client deletion blocked |
| `client_project_mappings.project_id -> projects(id)` | CASCADE | Mappings die with project |
| `project_assignments.project_id -> projects(id)` | CASCADE | Assignments die with project |
| `project_assignments.user_id -> users(id)` | RESTRICT (explicit) | User deletion blocked |
| `client_features.client_id -> clients(id)` | RESTRICT | Client deletion blocked |
| `client_features.feature_id -> features(id)` | RESTRICT | Feature deletion blocked |
| `user_feature_overrides.user_id -> users(id)` | CASCADE | Overrides die with user |
| `user_feature_overrides.feature_id -> features(id)` | RESTRICT | Feature deletion blocked |
| `resources.module_id -> modules(id)` | (none = RESTRICT) | Module deletion blocked |
| `permissions.resource_id -> resources(id)` | (none = RESTRICT) | Resource deletion blocked |
| `permissions.action_id -> actions(id)` | (none = RESTRICT) | Action deletion blocked |
| `role_permissions.role_id -> roles(id)` | CASCADE | Role perms die with role |
| `role_permissions.permission_id -> permissions(id)` | CASCADE | Role perms die with permission |

**Inconsistency:** `user_permission_overrides.user_id` uses implicit RESTRICT (user deletion blocked), but `user_feature_overrides.user_id` uses explicit CASCADE (overrides auto-deleted). Both are "user override" tables with identical semantics.

Similarly, `user_roles.user_id` and `user_roles.role_id` have no explicit ON DELETE clause, defaulting to NO ACTION (which behaves like RESTRICT in practice). This should be explicitly documented.

---

### MEDIUM-04: Missing CHECK Constraint on users.is_active / Enum Mismatch

**Severity:** MEDIUM
**Impact:** Data integrity gap; no CHECK on boolean status columns

The `users` table uses `is_active BOOLEAN` while all Portal Routing tables use `status VARCHAR(20) CHECK (status IN ('Active', 'Inactive'))`. The `modules` table also uses `is_active BOOLEAN`. The `roles` table uses `is_active BOOLEAN`.

This means the codebase has TWO patterns for representing entity status:
1. `is_active BOOLEAN` (IAM domain) -- `True`/`False`
2. `status VARCHAR(20)` (Portal Routing + Feature Flags domain) -- `'Active'`/`'Inactive'`

The StatusConstants in C# use `"Active"` and `"Inactive"` strings. This means IAM entities need a mapping layer that Portal Routing entities do not.

---

### MEDIUM-05: position_types Table Missing created_by/updated_by Audit Columns

**Severity:** MEDIUM
**Impact:** Cannot track who modified position types
**File:** `Migrations/03-portal-routing-schema.sql` lines 189-203

The `position_types` table has `created_at` and `updated_at` but NO `created_by` or `updated_by`. Every other non-junction table in the schema has these columns.

---

### MEDIUM-06: user_sessions Table Missing Indexes on Commonly Queried Columns

**Severity:** MEDIUM
**Impact:** Token lookup and session cleanup queries will do sequential scans
**File:** `Migrations/01-portal-schema.sql`

No indexes are created for:
- `user_sessions.user_id` -- needed for "get all sessions for user"
- `user_sessions.expires_at` -- needed for session cleanup
- `user_sessions.is_revoked` -- needed for filtering active sessions

The `UAM-Schema.sql` reference file includes `idx_user_sessions_uid` and `idx_user_sessions_tkn`, but these are not in the authoritative migration 01.

---

### MEDIUM-07: audit_log Table Uses TIMESTAMPTZ While All Others Use TIMESTAMP

**Severity:** MEDIUM
**Impact:** JOIN operations between audit_log and other tables may produce unexpected time comparisons
**File:** `Migrations/01-portal-schema.sql`, line 194

```sql
timestamp TIMESTAMPTZ NOT NULL DEFAULT now(),  -- Uses TIMESTAMPTZ
```

All other tables use `TIMESTAMP WITHOUT TIME ZONE`. Mixing these types in queries (e.g., "find audit entries around the time this entity was created") requires explicit casting and can produce incorrect results depending on the session timezone.

---

### MEDIUM-08: Migration 02 Seed Position Types Without ON CONFLICT on Full Primary Key

**Severity:** MEDIUM
**Impact:** Seed may fail or produce duplicates in edge cases
**File:** `Migrations/02-portal-seed.sql`, lines 16-23

```sql
INSERT INTO position_types (name, code, sort_order) VALUES
    ('Technical PIC', 'TECH_PIC', 1), ...
ON CONFLICT (code) DO NOTHING;
```

The `position_types` table uses `code VARCHAR(20) PRIMARY KEY`. The `ON CONFLICT (code)` targets the PK correctly. However, the `sort_order INT NOT NULL UNIQUE` constraint means that if someone modifies a position type's sort order and then re-runs the seed, the constraint could prevent insertion of OTHER position types with the original sort order values. The DO NOTHING makes this silent.

---

### MEDIUM-09: No Explicit NOT NULL on Feature Flag Audit Columns

**Severity:** MEDIUM
**Impact:** Potential orphaned audit references
**File:** `Migrations/09-feature-flags.sql`

The `created_by` columns in feature flag tables are all `NULL`able. While this is necessary for system-generated records and test compatibility, it means there is no enforcement that user-created records must have a `created_by`. Combined with CRITICAL-01 (FK on audit columns), this creates a weak audit trail.

---

## LOW Findings

### LOW-01: Duplicate File -- actions copy.sql

**Severity:** LOW
**File:** `Tables/actions copy.sql`

This is an exact duplicate of `Tables/actions.sql`. Should be deleted to avoid confusion.

---

### LOW-02: UAM-Tenantless.sql Is Not SQL

**Severity:** LOW
**File:** `Tables/UAM-Tenantless.sql`

This file contains markdown discussion about architecture decisions, not SQL. It should be moved to `READMEs/` or renamed to `.md`.

---

### LOW-03: Inconsistent Column Type for Status Across Tables

**Severity:** LOW

| Column Type | Tables |
|-------------|--------|
| `VARCHAR(20)` | clients, projects, client_project_mappings, position_types, features (CHECK constrained) |
| `BOOLEAN` | users (is_active), modules (is_active), roles (is_active) |
| `VARCHAR(20)` unchecked | audit_log (none -- no status column) |

The `client_features` table uses `is_enabled BOOLEAN` instead of `status VARCHAR(20)`, introducing a third pattern. While semantically different (enabled/disabled vs active/inactive), it adds cognitive load.

---

### LOW-04: audit_log.user_id and client_id Are TEXT, Not UUID

**Severity:** LOW
**File:** `Migrations/01-portal-schema.sql`, lines 187-188

```sql
user_id    TEXT NULL,
client_id  TEXT NULL,
```

These are stored as TEXT despite all other user_id/client_id columns being UUID. This prevents JOIN operations with the main tables and means no FK enforcement. Likely intentional (audit log should be decoupled), but the type inconsistency may cause bugs in reporting queries.

---

### LOW-05: Missing Comments on Migration 01 Tables

**Severity:** LOW

Migration 03 adds `COMMENT ON TABLE` for all its tables. Migration 01 does not add comments for any IAM table (users, modules, resources, actions, permissions, roles, etc.).

---

## INFO Findings

### INFO-01: UAM-Schema.sql Contains an Entirely Different Architecture

The `Tables/UAM-Schema.sql` file contains a full multi-tenant RBAC schema with `tenants`, `groups`, `group_members`, `group_roles`, and `access_audit`/`grant_audit` tables. None of these exist in the production schema (migrations 01-10). This appears to be a pre-production design that was superseded by the "de-tenanted" approach in the authoritative migrations.

This file should be archived or clearly marked as deprecated to prevent confusion.

---

### INFO-02: TestDatabaseInitializer Skips 6 Migrations

The test initializer only loads: 00, 01, 03, 09, 10.
It skips: 02 (seeds), 04 (test data), 05 (status migration), 06 (permission generation), 07 (override seeds), 08 (schema fix).

**Consequences:**
- Tests run against a schema that has NEVER had migration 05, 06, 07, 08 applied
- The `user_permission_overrides` table in tests is MISSING `updated_at`/`updated_by` columns (migration 08 not applied)
- No permissions, roles, or seed data exist in the test database
- Tests must self-seed all data, which is correct for isolation but means schema drift from production is invisible

---

### INFO-03: Trigger Function Uses Quoted Language Name

**File:** `Migrations/03-portal-routing-schema.sql`, line 325

```sql
$$ LANGUAGE 'plpgsql';
```

The PostgreSQL convention is to use unquoted language names: `LANGUAGE plpgsql`. The quoted form works but is deprecated in some style guides. Minor style issue.

---

### INFO-04: Feature Flag Seed Uses Hardcoded UUIDs (Intentional)

**File:** `Migrations/10-feature-flag-seed.sql`

The 4 module feature flags use hardcoded UUIDv7-like IDs (e.g., `019c4700-0001-7000-0000-000000000001`). This is intentional for deterministic seeding, but these are NOT real UUIDv7 values (the random bytes are all zeros). This is fine for seed data but should be documented as synthetic IDs.

---

## Remediation Priority

### Immediate (Before Next Feature Work)
1. **CRITICAL-01:** Remove FK constraints from all audit columns (created_by, updated_by, deleted_by). This is the single highest-value fix.
2. **HIGH-02:** Add update triggers for all 9 IAM tables missing them.
3. **HIGH-04:** Convert users email/SSO unique constraints to partial indexes.
4. **HIGH-05:** Convert all IAM unique constraints to partial indexes (zombie constraint pattern).

### Short-Term (Next Sprint)
5. **HIGH-01:** Add missing FK indexes on all tables.
6. **HIGH-03:** Add soft delete columns to actions, roles, permissions tables.
7. **MEDIUM-01:** Standardize timestamp defaults to explicit UTC across all tables.
8. **MEDIUM-03:** Standardize ON DELETE behavior (make user_permission_overrides match user_feature_overrides).

### Medium-Term (Tech Debt Backlog)
9. **CRITICAL-02/03:** Fix migration idempotency issues and conflicts.
10. **HIGH-06/07:** Fix seed data idempotency in migrations 09 and 10.
11. **MEDIUM-02:** Clean up Tables/ directory (delete duplicates, move non-SQL files).
12. **MEDIUM-04:** Consider standardizing on one status pattern (BOOLEAN vs VARCHAR).

---

*Review complete. 29 findings across 33 SQL files. 3 critical, 8 high, 9 medium, 5 low, 4 informational.*
