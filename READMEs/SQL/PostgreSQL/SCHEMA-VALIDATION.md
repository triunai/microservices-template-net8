# SCHEMA-VALIDATION.md -- Integrity Audit

**Auditor**: Nansen (Verification Agent)
**Date**: 2026-02-10
**Scope**: All migrations (00-10), all table definitions in `Tables/`
**Status**: COMPLETE

---

## 1. AUDIT COLUMN FK MATRIX (HIGHEST PRIORITY)

The core question: Which tables have hard FK constraints on `created_by`, `updated_by`, `deleted_by` pointing to `users(id)`, and which leave them as plain UUIDs?

### Answer: ALL tables with audit columns use FK constraints to `users(id)`.

This is a **pervasive, schema-wide pattern**, not isolated to feature flags.

| Table | created_by FK? | updated_by FK? | deleted_by FK? | Source Migration |
|-------|---------------|----------------|----------------|-----------------|
| `users` | YES `REFERENCES users(id)` | YES | YES | 01 |
| `user_sessions` | N/A (no audit cols) | N/A | N/A | 01 |
| `modules` | YES | YES | YES | 01 |
| `resources` | YES | YES | YES | 01 |
| `actions` | YES | YES | N/A (no deleted_by) | 01 |
| `permissions` | YES | YES | N/A (no deleted_by) | 01 |
| `roles` | YES | YES | N/A (no deleted_by) | 01 |
| `role_permissions` | N/A (junction, no audit) | N/A | N/A | 01 |
| `user_roles` | N/A (has assigned_by FK) | N/A | N/A | 01 |
| `user_permission_overrides` | YES | YES (added in 08) | N/A (no soft delete) | 01 + 08 |
| `audit_log` | N/A (user_id is TEXT) | N/A | N/A | 01 |
| `clients` | YES | YES | YES | 03 |
| `projects` | YES | YES | YES | 03 |
| `client_project_mappings` | YES | YES | YES | 03 |
| `position_types` | N/A (no created_by) | N/A | N/A | 03 |
| `project_assignments` | YES | YES | YES | 03 |
| `features` | YES | YES | YES | 09 |
| `client_features` | YES | YES | YES | 09 |
| `user_feature_overrides` | YES (created_by only) | N/A | N/A | 09 |

### CRITICAL Finding: Universal FK Constraint on Audit Columns

**Classification: CRITICAL**

Every single `created_by`, `updated_by`, `deleted_by` column across the entire schema has a hard FK constraint to `users(id)`. This means:

1. **You cannot INSERT into ANY table** (except `users` itself, `user_sessions`, `audit_log`, `role_permissions`, and `position_types`) without either:
   - Having a valid user row in `users`, OR
   - Setting `created_by`/`updated_by` to NULL

2. **You cannot DELETE a user** if that user's ID appears in ANY `created_by`, `updated_by`, or `deleted_by` column across ANY table. None of these FKs have ON DELETE CASCADE or ON DELETE SET NULL -- they all use the default ON DELETE RESTRICT (NO ACTION).

3. **Test seeding requires a user to exist first** -- this is exactly the bug that caused 49 integration test failures.

### Cascading Consequences

**Can you ever hard-delete a user?**
NO. Not without first NULLing out every `created_by`, `updated_by`, `deleted_by` reference across 12+ tables. The only tables with `ON DELETE CASCADE` for user references are:
- `user_sessions.user_id` -> CASCADE
- `user_feature_overrides.user_id` -> CASCADE

Everything else is RESTRICT (default).

**What about soft-delete?**
Soft-delete works because it does not actually remove the row from `users`. The FK constraint remains satisfied. This is consistent with the project convention.

**But**: If you ever need to GDPR-purge a user (hard delete), you have a massive cascading update problem. Every audit column across every table would need to be SET NULL first.

---

## 2. ON DELETE BEHAVIOR MATRIX

| FK Relationship | ON DELETE | Assessment |
|-----------------|-----------|------------|
| `user_sessions.user_id` -> `users(id)` | CASCADE | OK -- sessions die with user |
| `modules.created_by` -> `users(id)` | RESTRICT (default) | WARNING -- blocks user deletion |
| `resources.module_id` -> `modules(id)` | RESTRICT (default) | WARNING -- no cascade specified |
| `clients.created_by` -> `users(id)` | RESTRICT (default) | WARNING -- blocks user deletion |
| `projects.client_id` -> `clients(id)` | RESTRICT | OK -- intentional, documented |
| `client_project_mappings.project_id` -> `projects(id)` | CASCADE | OK -- routes die with project |
| `project_assignments.project_id` -> `projects(id)` | CASCADE | OK -- assignments die with project |
| `project_assignments.user_id` -> `users(id)` | RESTRICT | OK -- intentional, documented |
| `client_features.client_id` -> `clients(id)` | RESTRICT | OK -- intentional |
| `client_features.feature_id` -> `features(id)` | RESTRICT | OK -- intentional |
| `user_feature_overrides.user_id` -> `users(id)` | CASCADE | OK -- overrides die with user |
| `user_feature_overrides.feature_id` -> `features(id)` | RESTRICT | OK -- intentional |
| `role_permissions.role_id` -> `roles(id)` | CASCADE | OK |
| `role_permissions.permission_id` -> `permissions(id)` | CASCADE | OK |
| `user_roles.user_id` -> `users(id)` | RESTRICT (default) | WARNING -- blocks user deletion |
| `user_roles.role_id` -> `roles(id)` | RESTRICT (default) | WARNING -- blocks role deletion |
| ALL audit column FKs | RESTRICT (default) | CRITICAL -- see Section 1 |

---

## 3. PRIMARY KEY ANALYSIS

| Table | PK Type | Default | Assessment |
|-------|---------|---------|------------|
| `users` | UUID | `uuid_generate_v7()` | OK |
| `user_sessions` | UUID | `uuid_generate_v7()` | OK |
| `modules` | UUID | `uuid_generate_v7()` | OK |
| `resources` | UUID | `uuid_generate_v7()` | OK |
| `actions` | UUID | `uuid_generate_v7()` | OK |
| `permissions` | UUID | `uuid_generate_v7()` | OK |
| `roles` | UUID | `uuid_generate_v7()` | OK |
| `role_permissions` | Composite (role_id, permission_id) | N/A | OK |
| `user_roles` | UUID | `uuid_generate_v7()` | OK |
| `user_permission_overrides` | UUID | `uuid_generate_v7()` | OK |
| `audit_log` | UUID | `uuid_generate_v7()` | OK |
| `clients` | UUID | `uuid_generate_v7()` | OK |
| `projects` | UUID | `uuid_generate_v7()` | OK |
| `client_project_mappings` | UUID | `uuid_generate_v7()` | OK |
| `position_types` | VARCHAR(20) code | N/A (natural key) | NOTE -- intentional design decision |
| `project_assignments` | UUID | `uuid_generate_v7()` | OK |
| `features` | UUID | `uuid_generate_v7()` | OK |
| `client_features` | UUID | `uuid_generate_v7()` | OK |
| `user_feature_overrides` | UUID | `uuid_generate_v7()` | OK |

**NOTE**: `position_types` uses a natural key (VARCHAR code) as PK, not UUID. This is documented as intentional in the migration comments. All other tables consistently use UUIDv7.

---

## 4. MIGRATION DEPENDENCY & TEST DB ANALYSIS

### Migrations Loaded by TestDatabaseInitializer

| # | File | Loaded in Tests? | Content |
|---|------|-----------------|---------|
| 00 | `00-extensions.sql` | YES | pgcrypto, uuid_generate_v7() polyfill |
| 01 | `01-portal-schema.sql` | YES | users, sessions, modules, resources, actions, permissions, roles, role_permissions, user_roles, user_permission_overrides, audit_log |
| 02 | `02-portal-seed.sql` | NO | actions seed, position_types seed, modules/resources seed, admin role, admin user, role assignment |
| 03 | `03-portal-routing-schema.sql` | YES | clients, projects, client_project_mappings, position_types, project_assignments, triggers |
| 04 | `04-test-data.sql` | NO | Test clients, projects, mappings, users, assignments |
| 05 | `05-standardize-status.sql` | NO | Migrates boolean is_active to varchar status on mappings + position_types |
| 06 | `06-seed-permissions.sql` | NO | Cross-join permissions generation |
| 07 | `07-seed-overrides.sql` | NO | Test override data for admin user |
| 08 | `08-fix-overrides-schema.sql` | NO | Adds updated_at/updated_by to user_permission_overrides |
| 09 | `09-feature-flags.sql` | YES | features, client_features, user_feature_overrides + 2 seed rows |
| 10 | `10-feature-flag-seed.sql` | YES | 4 module feature flag seeds (fixed UUIDs) |

### CRITICAL: Skipped Migration Impact Analysis

**Migration 02 (portal-seed) -- SKIPPED**
- Seeds: actions (VIEW, INSERT, EDIT, DELETE), position_types (6 codes), modules (3), resources (5), admin role, admin user
- **Impact**: Test DB has NO actions, NO position_types seed, NO modules, NO resources, NO roles, NO admin user
- **Blast radius**: Any test that needs to INSERT into `project_assignments` will FAIL because `position_types` has no rows (FK violation on `position_code`). However, migration 03 also creates the `position_types` table schema.
- **CRITICAL CONFLICT**: Migration 02 seeds position_types with `(name, code, sort_order)` but migration 03 creates a DIFFERENT `position_types` table with `code VARCHAR(20) PRIMARY KEY`. Since 03 runs after 02 in prod but 02 is skipped in tests, the test DB gets the 03 version (code as PK). In prod, 02 tries to INSERT into position_types that was created in... wait -- 02 references `position_types` which is only created in 03. THIS IS A BUG IN PROD MIGRATION ORDER.

**CRITICAL Finding: Migration 02 references `position_types` table which does not exist until Migration 03**

Migration 02, line 16: `INSERT INTO position_types (name, code, sort_order) VALUES ...`
Migration 03 is where `CREATE TABLE position_types` exists.

If migrations run in order (00, 01, 02, 03...), migration 02 will FAIL with `relation "position_types" does not exist`. This means either:
- (a) Production was set up by running 03 before 02 (manual intervention), OR
- (b) Production ran all at once via a combined script, OR
- (c) This is a known issue that was worked around

**Migration 05 (standardize-status) -- SKIPPED**
- Adds `status VARCHAR(20)` column to `client_project_mappings` and `position_types`, drops `is_active`
- **Impact**: Test DB `client_project_mappings` was created by migration 03 which ALREADY includes `status VARCHAR(20)` and CHECK constraint. So migration 03 is the "hardened" version that incorporated 05's changes.
- **Impact on position_types**: Migration 03 creates position_types WITHOUT a `status` column at all (it has no is_active either). Migration 05 would ADD `status`. Since 05 is skipped, test DB position_types has NO status column.
- **WAIT**: Re-reading migration 03 -- position_types DOES have `status VARCHAR(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active', 'Inactive'))`. So migration 03 is already the hardened version. Migration 05 is redundant for 03-created tables. This suggests 03 was written AFTER 05 was designed (the "PLATINUM HARDENED" version).

**Migration 06 (seed-permissions) -- SKIPPED**
- Cross-joins resources x actions to generate permissions
- **Impact**: Test DB has NO permissions rows. Tests involving permission checks, role_permissions, or user_permission_overrides that reference permissions will fail with FK violations.
- **Blast radius**: Low for current tests (feature flag tests do not touch permissions). But any future RBAC integration test will need this.

**Migration 07 (seed-overrides) -- SKIPPED**
- Seeds override test data for admin user
- **Impact**: Minimal -- only test data.

**Migration 08 (fix-overrides-schema) -- SKIPPED**
- Adds `updated_at` and `updated_by` columns to `user_permission_overrides`
- **Impact**: Test DB `user_permission_overrides` table is MISSING `updated_at` and `updated_by` columns.
- **CRITICAL**: If any test or DAC writes to `user_permission_overrides.updated_at`, it will fail with a column-not-found error.

---

## 5. CONSTRAINT ANALYSIS

### CHECK Constraints

| Table | Column | Constraint | Assessment |
|-------|--------|------------|------------|
| `clients` | `status` | `CHECK (status IN ('Active', 'Inactive'))` | OK |
| `projects` | `status` | `CHECK (status IN ('Active', 'Inactive'))` | OK |
| `client_project_mappings` | `status` | `CHECK (status IN ('Active', 'Inactive'))` | OK |
| `client_project_mappings` | `environment` | `CHECK (environment IN ('Production', 'Staging', 'Development', 'UAT'))` | OK |
| `client_project_mappings` | `routing_url` | `CHECK (routing_url ~ '^/[a-z0-9_-]+/[a-z0-9_-]+')` | OK |
| `position_types` | `status` | `CHECK (status IN ('Active', 'Inactive'))` | OK |
| `features` | `code` | `CHECK (code = upper(code) AND code ~ '^[A-Z][A-Z0-9_]*$')` | OK -- good pattern |
| `user_feature_overrides` | `override_state` | `CHECK (override_state IN ('FORCE_ON', 'FORCE_OFF'))` | OK |

**NOTE**: Tables from migration 01 (users, modules, resources, actions, permissions, roles, user_roles, user_permission_overrides) have NO CHECK constraints on status-like fields. They use `BOOLEAN is_active` instead of `VARCHAR status`. This is an inconsistency with the 03/09 tables.

### Missing CHECK Constraints

- `users.is_active` -- boolean, no CHECK needed
- `roles.is_active` -- boolean, no CHECK needed
- `modules.is_active` -- boolean, no CHECK needed, but INCONSISTENT with clients/projects using VARCHAR status
- `features.is_active` -- boolean, inconsistent naming vs other boolean toggles? Actually this is the "kill switch" so boolean makes sense here.

### Unique Constraints and Partial Indexes (Zombie Constraint Pattern)

| Table | Type | Columns | WHERE clause | Assessment |
|-------|------|---------|-------------|------------|
| `users` | UNIQUE constraint | `email` | NONE | WARNING -- not soft-delete-aware |
| `users` | UNIQUE constraint | `sso_provider, external_id` | NONE | WARNING -- not soft-delete-aware |
| `modules` | UNIQUE constraint | `code` | NONE | WARNING -- not soft-delete-aware |
| `actions` | UNIQUE constraint | `code` | NONE | WARNING -- not soft-delete-aware |
| `permissions` | UNIQUE constraint | `resource_id, action_id` | NONE | OK -- no soft delete on permissions |
| `permissions` | UNIQUE constraint | `code` | NONE | OK -- no soft delete on permissions |
| `roles` | UNIQUE constraint | `code` | NONE | WARNING -- not soft-delete-aware |
| `user_roles` | UNIQUE constraint | `user_id, role_id` | NONE | OK -- no soft delete |
| `user_permission_overrides` | UNIQUE constraint | `user_id, permission_id` | NONE | OK -- no soft delete |
| `resources` | UNIQUE constraint | `module_id, code` | NONE | WARNING -- not soft-delete-aware |
| `clients` | PARTIAL INDEX | `code` | `WHERE is_deleted = FALSE` | OK -- zombie-safe |
| `projects` | PARTIAL INDEX | `client_id, code` | `WHERE is_deleted = FALSE` | OK -- zombie-safe |
| `client_project_mappings` | PARTIAL INDEX | `routing_url` | `WHERE is_deleted = FALSE` | OK -- zombie-safe |
| `project_assignments` | PARTIAL INDEX | `project_id, user_id, position_code` | `WHERE is_deleted = FALSE` | OK -- zombie-safe |
| `features` | PARTIAL INDEX | `code` | `WHERE is_deleted = FALSE` | OK -- zombie-safe |
| `client_features` | PARTIAL INDEX | `client_id, feature_id` | `WHERE is_deleted = FALSE` | OK -- zombie-safe |
| `user_feature_overrides` | UNIQUE INDEX | `user_id, feature_id` | NONE | OK -- hard delete table |

**WARNING: Migration 01 Tables Lack Zombie-Safe Uniqueness**

Tables created in migration 01 (`users`, `modules`, `resources`, `actions`, `roles`) use plain UNIQUE constraints, NOT partial indexes. If you soft-delete a user with email `x@y.com`, you CANNOT create a new user with that same email. This is inconsistent with the 03/09 migration pattern.

Affected tables:
- `users.email` -- UNIQUE constraint, not partial index
- `users.sso_provider + external_id` -- UNIQUE constraint, not partial index
- `modules.code` -- UNIQUE constraint, not partial index
- `resources.module_id + code` -- UNIQUE constraint, not partial index
- `actions.code` -- UNIQUE constraint, not partial index (but actions have no soft delete, so this is OK)
- `roles.code` -- UNIQUE constraint, not partial index (roles have no soft delete column, so this is OK)

---

## 6. TRIGGER ANALYSIS

### update_updated_at_column() Trigger Function

Defined in migration 03. Applied to:

| Table | Trigger Exists? | Assessment |
|-------|----------------|------------|
| `clients` | YES | OK |
| `projects` | YES | OK |
| `client_project_mappings` | YES | OK |
| `position_types` | YES | OK |
| `project_assignments` | YES | OK |
| `users` | NO | WARNING -- missing |
| `modules` | NO | WARNING -- missing |
| `resources` | NO | WARNING -- missing |
| `actions` | NO | WARNING -- missing |
| `permissions` | NO | WARNING -- missing |
| `roles` | NO | WARNING -- missing |
| `user_roles` | NO | N/A (no updated_at) |
| `user_permission_overrides` | NO | WARNING -- has updated_at (from migration 08), no trigger |
| `features` | NO | WARNING -- missing |
| `client_features` | NO | WARNING -- missing |
| `user_feature_overrides` | NO | N/A -- minimal audit, no updated_at |
| `audit_log` | NO | N/A -- append-only |

**WARNING: Trigger function defined in 03 but not applied to 01 or 09 tables**

Migration 01 tables (users, modules, resources, actions, permissions, roles) all have `updated_at` columns but NO trigger to auto-update them. The app layer must handle this manually.

Migration 09 tables (features, client_features) have `updated_at` columns but NO trigger. Same issue.

This is not necessarily a bug if the app layer sets `updated_at` explicitly in every UPDATE query. But it IS inconsistent with the 03 tables where the trigger handles it automatically.

---

## 7. TIMESTAMP CONSISTENCY

| Migration | Default Expression | Assessment |
|-----------|--------------------|------------|
| 01 | `DEFAULT now()` | WARNING -- includes timezone context of server |
| 03 | `DEFAULT (now() AT TIME ZONE 'utc')` | OK -- explicit UTC |
| 09 | `DEFAULT (now() AT TIME ZONE 'utc')` | OK -- explicit UTC |

**WARNING: Migration 01 uses `DEFAULT now()` while 03 and 09 use `DEFAULT (now() AT TIME ZONE 'utc')`**

The `now()` function returns `TIMESTAMP WITH TIME ZONE` in the server's timezone. When stored in a `TIMESTAMP WITHOUT TIME ZONE` column (as used in 03/09), the timezone is stripped. When using just `DEFAULT now()` with `TIMESTAMP` (no timezone spec as in 01), the behavior depends on the column type.

Migration 01 declares columns as just `TIMESTAMP` (equivalent to `TIMESTAMP WITHOUT TIME ZONE`). The value stored will be the server's local time, NOT necessarily UTC. If the server timezone is set to `Asia/Kuala_Lumpur` (as suggested in UAM-Schema.sql), then migration 01 tables store MYT timestamps while 03/09 tables store UTC timestamps.

This is a **data inconsistency risk** across tables.

---

## 8. NAMING CONVENTIONS

### snake_case Compliance: PASS
All column and table names use snake_case consistently.

### Table Naming: Plural -- PASS (mostly)
All tables use plural names: `users`, `modules`, `resources`, `actions`, `permissions`, `roles`, `clients`, `projects`, `features`.
Exception: `audit_log` (singular). This is acceptable as a log table.

### Constraint Naming

| Pattern | Examples | Consistent? |
|---------|----------|-------------|
| `{table}_code_uk` | `modules_code_uk`, `roles_code_uk` | YES |
| `{table}_{cols}_uk` | `users_email_uk`, `user_roles_uk` | YES |
| `{table}_pk` | `role_permissions_pk` | YES |
| `{table}_{col}_chk` | `features_code_format_chk`, `user_feature_overrides_state_chk` | YES |
| `chk_{desc}` | `chk_routing_url_pattern` | NOTE -- different naming style |

**NOTE**: `chk_routing_url_pattern` in `client_project_mappings` uses a different naming convention (`chk_` prefix) than the migration 09 constraints (`{table}_{col}_chk` suffix). Minor inconsistency.

### Index Naming

| Pattern | Examples | Assessment |
|---------|----------|------------|
| `idx_{table}_{cols}` | `idx_users_email`, `idx_clients_status` | OK |
| `idx_{table}_{cols}_active` | `idx_clients_code_active`, `idx_features_code_active` | OK -- zombie indexes |

Generally consistent. No issues found.

---

## 9. IDEMPOTENCY ANALYSIS

| Migration | Can Run Twice Safely? | Mechanism | Assessment |
|-----------|-----------------------|-----------|------------|
| 00 | YES | `CREATE EXTENSION IF NOT EXISTS`, `DO $$ ... IF NOT EXISTS ...` | OK |
| 01 | NO | Bare `CREATE TABLE` | CRITICAL for tests, OK for prod (run once) |
| 02 | MOSTLY | `ON CONFLICT ... DO NOTHING`, but `DO $$` blocks would fail on duplicate INSERT with RETURNING | WARNING |
| 03 | NO | Bare `CREATE TABLE`, `CREATE INDEX` | CRITICAL for tests |
| 04 | YES | `WHERE NOT EXISTS` pattern throughout | OK |
| 05 | NO | Bare `ALTER TABLE ADD COLUMN` | Will fail if column already exists |
| 06 | YES | `ON CONFLICT ... DO NOTHING` | OK |
| 07 | YES | `ON CONFLICT ... DO NOTHING` | OK |
| 08 | NO | Bare `ALTER TABLE ADD COLUMN` | Will fail if column already exists |
| 09 | NO | Bare `CREATE TABLE` | Will fail if table exists |
| 10 | YES | `ON CONFLICT (id) DO NOTHING` | OK |

**NOTE**: Non-idempotent migrations are acceptable for production (run-once semantics). But for test environments, the TestDatabaseInitializer runs these on a fresh container each time, so idempotency is not strictly needed there either.

---

## 10. SUMMARY OF FINDINGS

### CRITICAL (Must Fix)

1. **Audit column FKs block user deletion**: Every audit column across 12+ tables has a hard FK to `users(id)` with default RESTRICT. You cannot hard-delete a user without first NULLing out all audit references. This is by design for soft-delete, but creates a GDPR liability if hard-deletion is ever needed.

2. **Migration 02 references `position_types` before it exists**: Migration 02 tries to INSERT INTO `position_types` which is only created in migration 03. Production migration order is broken unless manually reordered.

3. **Missing `updated_at`/`updated_by` on `user_permission_overrides` in test DB**: Migration 08 adds these columns but is skipped in tests. Any test touching these columns will fail.

### WARNING (Should Fix)

4. **Migration 01 tables lack zombie-safe uniqueness**: `users.email`, `modules.code`, `resources.module_id+code` use plain UNIQUE constraints, not partial indexes. Soft-deleting and recreating with the same unique values will fail.

5. **Missing update triggers on 01/09 tables**: `users`, `modules`, `resources`, `actions`, `permissions`, `roles`, `features`, `client_features` all have `updated_at` but no auto-update trigger. Relies on app layer.

6. **Timestamp default inconsistency**: Migration 01 uses `DEFAULT now()` while 03/09 use `DEFAULT (now() AT TIME ZONE 'utc')`. If server timezone is not UTC, data will be inconsistent.

7. **Inconsistent status modeling**: Migration 01 tables use `BOOLEAN is_active`, migration 03/09 tables use `VARCHAR status CHECK(...)`. Both conventions coexist.

### NOTE (Informational)

8. **`position_types` uses natural key**: VARCHAR `code` as PK instead of UUID. Intentional and documented.

9. **`audit_log.user_id` is TEXT, not UUID**: Unlike all other user references, `audit_log.user_id` is TEXT with no FK. This is likely intentional (audit logs should survive user deletion).

10. **`actions` and `permissions` tables have no soft delete**: They have `created_by`/`updated_by` FK columns but no `is_deleted`/`deleted_at`/`deleted_by`. This is consistent with being reference data.

11. **`roles` table has no soft delete columns**: Only `created_by`/`updated_by`, no `is_deleted`. Consistent with system reference data.

12. **Tables/ directory contains multiple historical versions**: `UAM-Schema.sql` is the old tenant-scoped version, `UAM-Tenantless.sql` is the discussion/rationale document. Neither matches the actual production schema in Migrations/.

13. **`actions copy.sql` in Tables/**: Appears to be an accidental duplicate file.

---

## 11. RECOMMENDATIONS BACK TO SHACKLETON

### R1: Decide on Audit Column FK Strategy (CRITICAL)
Options:
- (a) Keep as-is (accept that users are never hard-deleted -- soft-delete only)
- (b) Change audit FKs to `ON DELETE SET NULL` (allows hard deletion, loses audit trail)
- (c) Add a `SYSTEM_USER` sentinel row (UUID `00000000-...`) for seeding/testing, guarantee it's never deleted

Recommendation: Option (a) + (c). Keep FK constraints for integrity, add a well-known system user for seeding.

### R2: Fix Migration 02 Ordering (CRITICAL)
Either:
- Move `position_types` creation from 03 to 01 (or a new 01b), OR
- Move position_types seed from 02 to after 03, OR
- Document that 03 must run before 02 in production

### R3: Add Migration 08 to TestDatabaseInitializer (CRITICAL)
The test DB is missing `updated_at`/`updated_by` on `user_permission_overrides`. Add `08-fix-overrides-schema.sql` to the RequiredFiles array.

### R4: Evaluate Zombie-Safety for Migration 01 Tables (WARNING)
Either:
- Add partial indexes (replacing UNIQUE constraints) for `users.email`, `modules.code`, `resources.(module_id, code)`, OR
- Document that these entities use hard-delete semantics (no soft-delete reuse)

### R5: Add Update Triggers for 01/09 Tables (WARNING)
Or document that the app layer is responsible for setting `updated_at` on these tables.

### R6: Standardize Timestamp Defaults (WARNING)
Change migration 01's `DEFAULT now()` to `DEFAULT (now() AT TIME ZONE 'utc')` for consistency, or document the server timezone requirement.

---

## 12. APP LAYER CROSS-VALIDATION (DAC SQL Consistency)

### Confirmed: App DACs Manually Set `updated_at`

Verified that all WriteDac classes explicitly set `updated_at` in their SQL UPDATE statements. The missing DB triggers on migration 01/09 tables are therefore not a functional bug -- the app layer compensates. However, any raw SQL UPDATE bypassing the app layer (e.g., migration scripts, ad-hoc queries) will NOT auto-update the timestamp on 01/09 tables (while 03 tables would auto-update via trigger).

### WARNING: `now()` vs `NOW() AT TIME ZONE 'utc'` Inconsistency in DAC SQL

| DAC File | Timestamp Expression | Assessment |
|----------|---------------------|------------|
| `FeatureWriteDac.cs` | `NOW() AT TIME ZONE 'utc'` | OK |
| `ClientWriteDac.cs` | `NOW() AT TIME ZONE 'utc'` | OK |
| `ProjectWriteDac.cs` | `NOW() AT TIME ZONE 'utc'` | OK |
| `ClientProjectMappingWriteDac.cs` | `now()` (bare) | WARNING -- inconsistent |
| `UserWriteDac.cs` | `NOW() AT TIME ZONE 'utc'` (most), `@UpdatedAt` param (some) | Mixed |
| `RoleWriteDac.cs` | `NOW() AT TIME ZONE 'utc'` | OK |

`ClientProjectMappingWriteDac` uses bare `now()` on lines 67 and 97 for its UPDATE and soft-delete operations. If the PostgreSQL server timezone is not UTC, these timestamps will differ from the UTC-explicit timestamps used everywhere else.

### CONFIRMED: `user_permission_overrides.updated_at` Time Bomb

`UserWriteDac.cs` (line ~240) inserts into `user_permission_overrides` with columns `created_at, created_by, updated_at, updated_by`. These `updated_at`/`updated_by` columns only exist if migration 08 has been applied. Since TestDatabaseInitializer skips migration 08, any integration test exercising the permission override upsert path in `UserWriteDac` will fail with a column-not-found error.

This is currently dormant because no existing integration test calls this specific DAC method. But it is a guaranteed failure point for any future RBAC integration test.

---

## 13. TABLES/ DIRECTORY ARCHAEOLOGY

The `Tables/` directory contains reference/documentation files, NOT the production schema. Key observations:

| File | Status | Notes |
|------|--------|-------|
| `UAM-Schema.sql` | OBSOLETE | Multi-tenant version with `tenants` table. Does NOT match production. |
| `UAM-Tenantless.sql` | DISCUSSION DOC | Contains the rationale for removing tenants. Not executable SQL. |
| `users.sql` | MATCHES migration 01 | Standalone table definition, consistent. |
| `features.sql` | MATCHES migration 09 | Standalone table definition, consistent. |
| `client_features.sql` | MATCHES migration 09 | Standalone table definition, consistent. |
| `user_feature_overrides.sql` | MATCHES migration 09 | Standalone table definition, consistent. |
| `actions copy.sql` | DUPLICATE FILE | Should be deleted. Accidental copy. |
| Other table files | REFERENCE ONLY | Match their respective migration definitions. |

**NOTE**: The authoritative schema source is the Migrations/ directory. The Tables/ directory is supplementary documentation and may drift out of sync over time.
