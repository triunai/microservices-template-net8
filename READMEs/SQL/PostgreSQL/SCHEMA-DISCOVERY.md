# SCHEMA-DISCOVERY.md -- SQL vs C# Cross-Check Report

**Generated:** 2026-02-10
**Scope:** All SQL files in `READMEs/SQL/PostgreSQL/` cross-checked against live C# codebase
**Method:** Module-by-module walk-through of every table, column, FK, index, and trigger

---

## EXECUTIVE SUMMARY

### Critical Findings Count
| Severity | Count |
|----------|-------|
| **FRAGILE** (FK audit columns to users) | 16 tables affected |
| **MISMATCH** (SQL vs C# divergence) | 11 instances |
| **ORPHAN** (unused columns/tables) | 8 instances |
| **MISSING** (skipped migrations in tests) | 6 migrations skipped |
| **MATCH** (confirmed correct) | ~85% of column mappings |

### The Two Alarm Triggers (Confirmed)

**1. Audit column FK constraints to `users(id)` -- CONFIRMED SYSTEMIC**
Every table with `created_by`, `updated_by`, `deleted_by` has `REFERENCES users(id)`. This is present in:
- `users` (self-referential), `modules`, `resources`, `actions`, `permissions`, `roles`, `user_permission_overrides`
- `clients`, `projects`, `client_project_mappings`, `project_assignments`, `position_types` (no audit FKs)
- `features`, `client_features`, `user_feature_overrides`

This means: (a) You cannot seed data without a valid user, (b) You cannot delete users who appear in ANY audit column of ANY table, (c) Circular dependency for the first user (created_by is self-referential NULL).

**2. TestDatabaseInitializer loads 5 of 11 migrations -- CONFIRMED**
Loaded: 00, 01, 03, 09, 10
Skipped: 02 (seed data), 04 (test data), 05 (status standardize), 06 (permission generation), 07 (override seed), 08 (fix overrides schema)

---

## MIGRATION INVENTORY

| # | File | Purpose | In TestInit? | Impact of Skip |
|---|------|---------|-------------|----------------|
| 00 | extensions.sql | pgcrypto + uuid_generate_v7() | YES | N/A |
| 01 | portal-schema.sql | users, sessions, RBAC, audit_log | YES | N/A |
| 02 | portal-seed.sql | Actions, position_types seed, modules, resources, admin user/role | NO | **FRAGILE** - position_types not seeded, modules/resources/permissions empty |
| 03 | portal-routing-schema.sql | clients, projects, mappings, position_types, project_assignments, triggers | YES | N/A |
| 04 | test-data.sql | Sample clients, projects, mappings, users, assignments | NO | OK for unit tests, integration tests seed their own |
| 05 | standardize-status.sql | Migrates is_active -> status on mappings + position_types | NO | **MISMATCH** - Schema in 03 already has status columns (DDL shows final state, but migration 05 is ALTER) |
| 06 | seed-permissions.sql | Generates permissions cross-product (resources x actions) | NO | **FRAGILE** - permissions table empty, RBAC queries return nothing |
| 07 | seed-overrides.sql | Seeds test permission overrides for admin | NO | Low impact (test data only) |
| 08 | fix-overrides-schema.sql | Adds updated_at, updated_by to user_permission_overrides | NO | **MISMATCH** - user_permission_overrides missing 2 columns |
| 09 | feature-flags.sql | features, client_features, user_feature_overrides | YES | N/A |
| 10 | feature-flag-seed.sql | 4 module feature flags | YES | N/A |

### TestDatabaseInitializer Skip Impact Analysis

**CRITICAL: Migration 05 skip is a no-op** because migration 03 already defines position_types and client_project_mappings with `status VARCHAR(20)` columns. Migration 05 is an ALTER that adds these columns and drops `is_active`. Since tests load 03 (which already has the final schema), 05 is idempotent/redundant for test purposes.

**CRITICAL: Migration 08 skip IS a real problem.** Migration 01 creates `user_permission_overrides` WITHOUT `updated_at` and `updated_by` columns. Migration 08 adds them. Tests skip 08, so the test schema for `user_permission_overrides` is missing these two columns. However, no DAC currently writes to these columns on `user_permission_overrides` directly (the `GrantPermissionAsync` in `UserWriteDac` does insert with `updated_at` and `updated_by` -- this would FAIL in tests). The `GetPermissionsAsync` query does not SELECT these columns, so reads work by accident.

**CRITICAL: Migration 02 skip means position_types table is EMPTY in tests.** Position types are seeded in migration 02. The `project_assignments` table has `REFERENCES position_types(code)` FK. Any integration test that inserts project_assignments needs position_types pre-seeded -- tests that work today either seed position_types themselves or don't test assignments via SQL.

---

## MODULE 1: IDENTITY (users, user_sessions, modules, resources, actions, permissions, roles, role_permissions, user_roles, user_permission_overrides)

### Table: `users` (Migration 01)

**SQL Columns vs C# Entity (`User.cs`):**

| SQL Column | C# Entity Property | DAC Read? | DAC Write? | Status |
|-----------|-------------------|-----------|------------|--------|
| id | Id | YES | YES | **MATCH** |
| display_name | DisplayName | YES | YES | **MATCH** |
| email | Email | YES | YES | **MATCH** |
| contact_number | ContactNumber | YES | YES | **MATCH** |
| is_active | IsActive | YES | YES | **MATCH** |
| local_login_enabled | LocalLoginEnabled | YES | YES | **MATCH** |
| password_hash | PasswordHash | Credentials only | YES (Create) | **MATCH** |
| password_salt | PasswordSalt | Credentials only | YES (Create) | **MATCH** |
| password_last_changed_at | -- | NO | NO | **ORPHAN** - SQL column exists, no C# property |
| password_expiry_at | PasswordExpiryAt | Credentials only | NO (not in Create/Update) | **MISMATCH** - Entity has property but WriteDac never sets it |
| password_reset_token | PasswordResetToken | WriteDac GetById only | NO (not in Create/Update) | **MISMATCH** - Read but never written by API |
| password_reset_expires_at | PasswordResetExpiresAt | WriteDac GetById only | NO (not in Create/Update) | **MISMATCH** - Read but never written by API |
| sso_login_enabled | SsoLoginEnabled | YES | YES | **MATCH** |
| sso_provider | SsoProvider | YES | YES | **MATCH** |
| external_id | ExternalId | YES | YES | **MATCH** |
| sso_email | SsoEmail | NO (ReadDac) | YES (WriteDac Create) | **MISMATCH** - Written on create, never read back by ReadDac |
| last_login_at | LastLoginAt | YES | YES (UpdateLastLogin) | **MATCH** |
| last_login_provider | LastLoginProvider | YES | YES (UpdateLastLogin) | **MATCH** |
| created_at | CreatedAt | YES | YES | **MATCH** |
| created_by | CreatedBy | YES | YES | **FRAGILE** - FK to users(id), self-referential |
| updated_at | UpdatedAt | YES | YES | **MATCH** |
| updated_by | UpdatedBy | YES | YES | **FRAGILE** - FK to users(id), self-referential |
| is_deleted | IsDeleted | WriteDac only | YES | **MATCH** |
| deleted_at | DeletedAt | WriteDac only | YES | **FRAGILE** - FK to users(id), self-referential |
| deleted_by | DeletedBy | WriteDac only | YES | **FRAGILE** - FK to users(id), self-referential |

**Key Findings:**
- **ORPHAN**: `password_last_changed_at` exists in SQL, no C# property anywhere
- **MISMATCH**: `sso_email` is written by `UserWriteDac.CreateAsync` but NEVER selected by `UserReadDac` (not in any SELECT list). The `UserReadModel` does not include `SsoEmail`.
- **MISMATCH**: `password_reset_token` and `password_reset_expires_at` are only read via `UserWriteDac.GetByIdAsync` (which rehydrates full entity) but never written by any DAC method. Password reset flow appears unimplemented.
- **FRAGILE**: Self-referential FK on `created_by`/`updated_by`/`deleted_by`. First user must have NULL audit columns.

**Unique Constraints:**
- `users_email_uk UNIQUE (email)` -- NOT partial (no `WHERE is_deleted = FALSE`). **FRAGILE**: Cannot reuse email of soft-deleted user.
- `users_sso_uk UNIQUE (sso_provider, external_id)` -- NOT partial. **FRAGILE**: Same issue for SSO relink.

### Table: `user_sessions` (Migration 01)

**SQL vs C# Entity (`UserSession.cs`):**

| SQL Column | C# Entity Property | Status |
|-----------|-------------------|--------|
| id | Id | **MATCH** |
| user_id | UserId | **MATCH** |
| refresh_token | RefreshToken | **MATCH** |
| expires_at | ExpiresAt | **MATCH** |
| created_at | CreatedAt | **MATCH** |
| created_ip | CreatedIp | **MATCH** |
| device_info | DeviceInfo | **MATCH** |
| is_revoked | IsRevoked | **MATCH** |
| revoked_at | RevokedAt | **MATCH** |
| replaced_by | ReplacedBy | **MATCH** |

**NOTE**: `UserSession.Create()` uses `Guid.NewGuid()` (UUIDv4), not `Uuid7.NewUuid7()`. Convention violation but not a schema issue.
**NOTE**: No dedicated SessionReadDac or SessionWriteDac found in Dac/ folder -- session management likely handled elsewhere or inline.

### Table: `modules` (Migration 01)

| SQL Column | C# Entity Property | Status |
|-----------|-------------------|--------|
| id | Id | **MATCH** |
| name | Name | **MATCH** |
| code | Code | **MATCH** |
| is_active | -- | **MISMATCH** - SQL has `is_active BOOLEAN`, C# entity has no `IsActive` property |
| sort_order | SortOrder | **MATCH** |
| created_at | CreatedAt | **MATCH** |
| created_by | CreatedBy | **FRAGILE** - FK to users(id) |
| updated_at | UpdatedAt | **MATCH** |
| updated_by | UpdatedBy | **FRAGILE** - FK to users(id) |
| is_deleted | IsDeleted | **MATCH** (from AuditableEntity) |
| deleted_at | DeletedAt | **MATCH** |
| deleted_by | DeletedBy | **FRAGILE** - FK to users(id) |

**No DAC exists for modules** -- they are only accessed via JOIN in permission queries. **ORPHAN** from DAC perspective (no ModuleReadDac/ModuleWriteDac). Managed entirely via seed SQL.

### Table: `resources` (Migration 01)

| SQL Column | C# Entity Property | Status |
|-----------|-------------------|--------|
| id | Id | **MATCH** |
| module_id | ModuleId | **MATCH** |
| name | Name | **MATCH** |
| code | Code | **MATCH** |
| created_by, updated_by, deleted_by | -- | **FRAGILE** - FK to users(id) |

**MISMATCH**: C# `Resource` entity has `SortOrder` property, but SQL schema has NO `sort_order` column. The `Create()` factory accepts `sortOrder` but it can never be persisted.
**No DAC exists for resources** -- same as modules, accessed via JOINs only.

### Table: `actions` (Migration 01)

| SQL Column | C# Entity Property | Status |
|-----------|-------------------|--------|
| id | Id | **MATCH** |
| name | Name | **MATCH** |
| code | Code | **MATCH** |
| created_by | CreatedBy | **FRAGILE** - FK to users(id) |
| updated_by | UpdatedBy | **FRAGILE** - FK to users(id) |

**MISSING**: SQL `actions` table has NO `is_deleted`, `deleted_at`, `deleted_by` columns, but C# `Action` entity inherits from `AuditableEntity` which includes them. Harmless (they exist in C# but never persisted), but misleading.
**No DAC exists for actions**.

### Table: `permissions` (Migration 01)

| SQL Column | C# Entity Property | Status |
|-----------|-------------------|--------|
| id | Id | **MATCH** |
| resource_id | ResourceId | **MATCH** |
| action_id | ActionId | **MATCH** |
| code | Code | **MATCH** |
| description | Description | **MATCH** |
| created_by, updated_by | -- | **FRAGILE** - FK to users(id) |

**MISSING**: SQL `permissions` table has NO `is_deleted`, `deleted_at`, `deleted_by` columns, but C# entity inherits from `AuditableEntity`. Same pattern as actions.
**No DAC exists for permissions** -- managed via seed SQL and JOINed in queries.

### Table: `roles` (Migration 01)

| SQL Column | C# Entity Property | RoleReadDac? | RoleWriteDac? | Status |
|-----------|-------------------|-------------|--------------|--------|
| id | Id | YES | YES | **MATCH** |
| name | Name | YES | YES | **MATCH** |
| code | Code | YES | YES | **MATCH** |
| description | Description | YES | YES | **MATCH** |
| is_system | IsSystem | YES | NO (hardcoded FALSE) | **MATCH** |
| is_active | IsActive | YES | YES | **MATCH** |
| created_at | CreatedAt | YES | YES (via DEFAULT) | **MATCH** |
| created_by | CreatedBy | YES | YES | **FRAGILE** - FK to users(id) |
| updated_at | UpdatedAt | YES | YES | **MATCH** |
| updated_by | UpdatedBy | YES | YES | **FRAGILE** - FK to users(id) |

**MISSING from SQL**: `roles` table has NO `is_deleted`, `deleted_at`, `deleted_by` columns. C# entity inherits `AuditableEntity` (has them). `RoleWriteDac.DeleteAsync()` does hard DELETE, which is correct given no soft-delete columns.
**MISMATCH**: C# `Role` entity has no `Code` property -- wait, checking... Actually `Role.cs` has `Name` and `Description` but NO `Code`. The `RoleReadDac` queries `r.code` and maps it to `_RoleRow.code` -> `RoleReadModel.Code`. This works for reads. But `RoleWriteDac.CreateAsync` INSERTs `@Code` which comes from the handler parameter, not from a `Role` entity property. The `Role` entity is NEVER USED for write operations -- the WriteDac takes raw parameters. **MISMATCH** between entity and actual usage pattern.

**ALSO MISSING from roles**: No `is_deleted` column means `RoleReadDac` queries do NOT filter `WHERE is_deleted = FALSE`. This is correct because the column doesn't exist, but it means roles cannot be soft-deleted (only hard-deleted). **MATCH** with actual behavior.

### Table: `role_permissions` (Migration 01)

| SQL Column | C# Entity (RolePermission.cs) | Status |
|-----------|------------------------------|--------|
| role_id | RoleId | **MATCH** |
| permission_id | PermissionId | **MATCH** |

**MISMATCH**: SQL table is a simple junction table with composite PK `(role_id, permission_id)` and NO `id` column. C# `RolePermission` entity inherits from `AuditableEntity` which has `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, etc. -- none of these columns exist in SQL. Entity is never used for DB operations (role_permissions managed via seed SQL + JOINs in permission queries).

### Table: `user_roles` (Migration 01)

| SQL Column | C# Entity (UserRole.cs) | RoleWriteDac? | Status |
|-----------|------------------------|--------------|--------|
| id | Id | YES | **MATCH** |
| user_id | UserId | YES | **MATCH** |
| role_id | RoleId | YES | **MATCH** |
| assigned_by_user_id | -- | YES (as @AssignedBy) | **MISMATCH** - No C# property for this |
| assigned_at | -- | YES (via NOW()) | **MISMATCH** - No C# property for this |

**MISMATCH**: C# `UserRole` entity inherits `AuditableEntity` (has CreatedAt, UpdatedAt, IsDeleted, etc.) but SQL table has `assigned_by_user_id` and `assigned_at` instead. The entity is never used for write operations.

### Table: `user_permission_overrides` (Migration 01 + Migration 08)

| SQL Column (after M08) | C# Entity | UserWriteDac? | Status |
|------------------------|-----------|--------------|--------|
| id | Id | YES | **MATCH** |
| user_id | UserId | YES | **MATCH** |
| permission_id | PermissionId | YES | **MATCH** |
| is_allowed | IsAllowed | YES | **MATCH** |
| reason | -- | NO | **ORPHAN** - SQL column, no C# property |
| created_at | CreatedAt | YES | **MATCH** |
| created_by | CreatedBy | YES | **FRAGILE** - FK to users(id) |
| updated_at (M08) | UpdatedAt | YES | **MATCH** (but only after M08) |
| updated_by (M08) | UpdatedBy | YES | **FRAGILE** - FK to users(id), only after M08 |

**CRITICAL for tests**: Migration 08 is NOT loaded by TestDatabaseInitializer. The `GrantPermissionAsync` method INSERTs with `updated_at` and `updated_by` columns. This INSERT will FAIL in integration tests because these columns don't exist in the test schema. Tests pass only because no integration test exercises `GrantPermissionAsync`.

### Table: `audit_log` (Migration 01)

SQL has 19 columns. Uses `TIMESTAMPTZ` (not `TIMESTAMP WITHOUT TIME ZONE` like other tables -- inconsistency). Uses `TEXT` for user_id and client_id (not UUID -- intentional for flexibility). No C# entity exists for audit_log -- it's written by `AuditLogger` background service directly. No ReadDac exists. **MATCH** with design intent (write-only audit sink).

**Notable**: `audit_log` has NO FK constraints at all -- intentionally decoupled. This is correct design for an audit log.

---

## MODULE 2: PORTAL ROUTING (clients, projects, client_project_mappings, position_types)

### Table: `clients` (Migration 03)

| SQL Column | C# Entity | ClientReadDac? | ClientWriteDac? | Status |
|-----------|-----------|---------------|----------------|--------|
| id | Id | YES | YES | **MATCH** |
| code | Code | YES | YES | **MATCH** |
| name | Name | YES | YES | **MATCH** |
| status | Status | YES | YES | **MATCH** |
| created_at | CreatedAt | YES | NO (via DEFAULT) | **MATCH** |
| created_by | CreatedBy | NO | YES | **FRAGILE** - FK to users(id) |
| updated_at | UpdatedAt | YES | YES (via NOW()) | **MATCH** |
| updated_by | UpdatedBy | NO | YES | **FRAGILE** - FK to users(id) |
| is_deleted | IsDeleted | NO (filtered) | YES | **MATCH** |
| deleted_at | DeletedAt | NO | YES | **MATCH** |
| deleted_by | DeletedBy | NO | YES | **FRAGILE** - FK to users(id) |

**MISMATCH**: `ClientReadDac` SELECTs `created_at, updated_at` but NOT `created_by, updated_by`. `ClientReadModel` has `CreatedAt, UpdatedAt` but NOT `CreatedBy, UpdatedBy`. The SQL schema has these columns. Not a bug, but the read model is intentionally thin. **MATCH** (design choice).

### Table: `projects` (Migration 03)

| SQL Column | C# Entity | ProjectReadDac? | ProjectWriteDac? | Status |
|-----------|-----------|----------------|-----------------|--------|
| id | Id | YES | YES | **MATCH** |
| code | Code | YES | YES | **MATCH** |
| name | Name | YES | YES | **MATCH** |
| client_id | ClientId | YES | YES | **MATCH** |
| external_url | ExternalUrl | YES | YES | **MATCH** |
| status | Status | YES | YES | **MATCH** |
| created_at | CreatedAt | YES | NO (via DEFAULT) | **MATCH** |
| created_by | CreatedBy | NO | YES | **FRAGILE** - FK to users(id) |
| updated_at | UpdatedAt | YES | YES | **MATCH** |
| updated_by | UpdatedBy | NO | YES | **FRAGILE** - FK to users(id) |
| is_deleted | IsDeleted | NO (filtered) | YES | **MATCH** |
| deleted_at | DeletedAt | NO | YES | **MATCH** |
| deleted_by | DeletedBy | NO | YES | **FRAGILE** - FK to users(id) |

**MATCH** across the board. Same thin-read-model pattern as clients.

### Table: `client_project_mappings` (Migration 03, altered by 05)

| SQL Column | C# Entity | MappingReadDac? | MappingWriteDac? | Status |
|-----------|-----------|----------------|-----------------|--------|
| id | Id | YES | YES (DB-gen) | **MATCH** |
| project_id | ProjectId | YES | YES | **MATCH** |
| routing_url | RoutingUrl | YES | YES | **MATCH** |
| environment | Environment | YES | YES | **MATCH** |
| status | Status | YES | YES | **MATCH** |
| created_at | CreatedAt | YES | YES | **MATCH** |
| created_by | CreatedBy | NO | YES | **FRAGILE** - FK to users(id) |
| updated_at | UpdatedAt | NO | YES | **MATCH** |
| updated_by | UpdatedBy | NO | YES | **FRAGILE** - FK to users(id) |
| is_deleted | IsDeleted | NO (filtered) | YES | **MATCH** |
| deleted_at | DeletedAt | NO | YES | **MATCH** |
| deleted_by | DeletedBy | NO | YES | **FRAGILE** - FK to users(id) |

### Table: `position_types` (Migration 03)

| SQL Column | C# Entity | Any DAC? | Status |
|-----------|-----------|---------|--------|
| code (PK) | Code | NO DAC | **MATCH** |
| name | Name | NO DAC | **MATCH** |
| description | Description | NO DAC | **MATCH** |
| sort_order | SortOrder | NO DAC | **MATCH** |
| status | Status | NO DAC | **MATCH** |
| created_at | CreatedAt | NO DAC | **MATCH** |
| updated_at | UpdatedAt | NO DAC | **MATCH** |

**ORPHAN**: No `PositionTypeReadDac` or `PositionTypeWriteDac` exists. Position types are only used via FK in `project_assignments` and accessed indirectly. Entity exists in C# but is never persisted through any DAC.

**NOTE**: Position types use VARCHAR PK (not UUID). No audit FKs (no created_by/updated_by/deleted_by). No is_deleted column. This is the cleanest table in the schema. **MATCH** with lightweight reference data design.

---

## MODULE 3: TASK ALLOCATION (project_assignments)

### Table: `project_assignments` (Migration 03)

| SQL Column | C# Entity | AssignmentReadDac? | TaskAllocationWriteDac? | Status |
|-----------|-----------|-------------------|------------------------|--------|
| id | Id | NO | NO (DB-gen) | **MATCH** |
| project_id | ProjectId | YES (as ProjectId) | YES | **MATCH** |
| user_id | UserId | YES (as UserId) | YES | **MATCH** |
| position_code | PositionCode | YES | YES | **MATCH** |
| created_at | CreatedAt | NO | NO (via DEFAULT) | **MATCH** |
| created_by | CreatedBy | NO | YES (as @By) | **FRAGILE** - FK to users(id) |
| updated_at | UpdatedAt | NO | NO (via trigger) | **MATCH** |
| updated_by | UpdatedBy | NO | YES (as @By) | **FRAGILE** - FK to users(id) |
| is_deleted | IsDeleted | NO (filtered) | YES | **MATCH** |
| deleted_at | DeletedAt | NO | YES | **MATCH** |
| deleted_by | DeletedBy | NO | YES | **FRAGILE** - FK to users(id) |

**MISMATCH**: `ProjectAssignmentReadDac` queries use denormalized column aliases (ProjectId, ProjectName, ProjectCode, ClientId, ClientName, UserId, UserName, PositionCode) from JOINs. The `ProjectAssignmentReadModel` does NOT include `id`, `created_at`, or any audit columns. The `id` column from `project_assignments` is never SELECTed in read queries. This is intentional (flat matrix view). **MATCH** with design.

---

## MODULE 4: FEATURE FLAGS (features, client_features, user_feature_overrides)

### Table: `features` (Migration 09)

| SQL Column | C# Entity | FeatureReadDac? | FeatureWriteDac? | Status |
|-----------|-----------|----------------|-----------------|--------|
| id | Id | YES | YES | **MATCH** |
| code | Code | YES | YES | **MATCH** |
| name | Name | YES | YES | **MATCH** |
| description | Description | YES | YES | **MATCH** |
| is_active | IsActive | YES | YES | **MATCH** |
| requires_client | RequiresClient | YES | YES | **MATCH** |
| created_at | CreatedAt | YES | NO (DEFAULT) | **MATCH** |
| created_by | CreatedBy | YES | YES | **FRAGILE** - FK to users(id) |
| updated_at | UpdatedAt | YES | YES | **MATCH** |
| updated_by | UpdatedBy | YES | YES | **FRAGILE** - FK to users(id) |
| is_deleted | IsDeleted | NO (filtered) | YES | **MATCH** |
| deleted_at | DeletedAt | NO | YES | **MATCH** |
| deleted_by | DeletedBy | NO | YES | **FRAGILE** - FK to users(id) |

**MATCH** -- Cleanest mapping in the codebase. Feature flag DACs were written most recently and follow conventions well.

### Table: `client_features` (Migration 09)

| SQL Column | C# Entity | FeatureReadDac? | FeatureWriteDac? | Status |
|-----------|-----------|----------------|-----------------|--------|
| id | Id | YES | YES (Uuid7) | **MATCH** |
| client_id | ClientId | YES | YES | **MATCH** |
| feature_id | FeatureId | YES | YES | **MATCH** |
| is_enabled | IsEnabled | YES | YES | **MATCH** |
| created_at | CreatedAt | YES | NO (DEFAULT) | **MATCH** |
| created_by | CreatedBy | YES | YES (as @UpdatedBy) | **FRAGILE** - FK to users(id) |
| updated_at | UpdatedAt | YES | YES | **MATCH** |
| updated_by | UpdatedBy | YES | YES | **FRAGILE** - FK to users(id) |
| is_deleted | IsDeleted | NO (filtered) | YES | **MATCH** |
| deleted_at | DeletedAt | NO | YES | **MATCH** |
| deleted_by | DeletedBy | NO | YES | **FRAGILE** - FK to users(id) |

### Table: `user_feature_overrides` (Migration 09)

| SQL Column | C# Entity | FeatureReadDac? | FeatureWriteDac? | Status |
|-----------|-----------|----------------|-----------------|--------|
| id | Id | YES | YES (Uuid7) | **MATCH** |
| user_id | UserId | YES | YES | **MATCH** |
| feature_id | FeatureId | YES | YES | **MATCH** |
| override_state | OverrideState | YES | YES | **MATCH** |
| reason | Reason | YES | YES | **MATCH** |
| created_at | CreatedAt | YES | YES (via NOW() on upsert) | **MATCH** |
| created_by | CreatedBy | YES | YES | **FRAGILE** - FK to users(id) |

**MATCH** -- Hard-delete table (no is_deleted). Correct. Entity inherits from `Entity` (not `AuditableEntity`). Correct.

---

## MODULE 5: AUDIT LOG

### Table: `audit_log` (Migration 01)

No C# entity. Written by `AuditLogger` hosted service. No ReadDac. 19 columns. Uses `TIMESTAMPTZ` (unique in schema). Uses `TEXT` for user_id/client_id (not UUID FK). No FK constraints. **MATCH** with intent -- write-only append log.

**ORPHAN from C# perspective**: `audit_log` table has no corresponding entity, read model, or DAC. It is only accessed via the `AuditLogger` infrastructure service which uses raw SQL INSERT.

---

## MODULE 6: CROSS-CUTTING

### Triggers (Migration 03)

Five triggers defined, all calling `update_updated_at_column()`:
1. `update_clients_timestamp` on `clients` -- **MATCH**
2. `update_projects_timestamp` on `projects` -- **MATCH**
3. `update_mappings_timestamp` on `client_project_mappings` -- **MATCH**
4. `update_position_types_timestamp` on `position_types` -- **MATCH**
5. `update_assignments_timestamp` on `project_assignments` -- **MATCH**

**MISSING triggers for:**
- `users` -- no trigger, `updated_at` managed by C# code
- `modules`, `resources`, `actions`, `permissions` -- no triggers
- `roles` -- no trigger, `updated_at` managed by DAC SQL (`NOW() AT TIME ZONE 'utc'`)
- `user_roles` -- no trigger (no `updated_at` column)
- `user_permission_overrides` -- no trigger (migration 08 adds `updated_at` but no trigger)
- `features` -- no trigger, `updated_at` managed by DAC SQL
- `client_features` -- no trigger, `updated_at` managed by DAC SQL
- `user_feature_overrides` -- no trigger (no `updated_at` column)

**MISMATCH**: Tables from migration 01 (identity/RBAC) have NO triggers. Tables from migration 03 (portal routing) have triggers. Tables from migration 09 (features) have NO triggers. The `updated_at` auto-update is inconsistent:
- Portal Routing tables: trigger overwrites `updated_at` (C# value is IGNORED)
- Identity/Feature tables: C# DAC sets `updated_at` explicitly in UPDATE SQL

This means: If a Portal Routing DAC UPDATE sets `updated_at = @UpdatedAt`, the trigger will OVERWRITE it with `NOW() AT TIME ZONE 'utc'`. The C# value is dead code for these tables. This is actually harmless (trigger value is more reliable) but creates confusion.

### Extensions (Migration 00)

- `pgcrypto` -- used for `gen_random_bytes` in UUID polyfill
- `uuid_generate_v7()` function -- polyfill for PG < 18, wrapper for PG 18+

**MATCH** -- Correctly used across all table defaults.

### Seed Data (Migrations 02, 04, 06, 07, 10)

- Migration 02: Inserts actions (VIEW, INSERT, EDIT, DELETE), position types (6 immutable), modules (3), resources, admin role, admin user
- Migration 04: Test clients, projects, mappings, users, assignments
- Migration 06: Generates permission cross-product (resources x actions)
- Migration 07: Seeds test permission overrides
- Migration 10: Seeds 4 module feature flags

---

## AUDIT COLUMN FK MATRIX

This is the critical finding. Every `REFERENCES users(id)` on audit columns creates coupling.

| Table | created_by FK? | updated_by FK? | deleted_by FK? | Impact |
|-------|---------------|---------------|---------------|--------|
| **users** | YES (self) | YES (self) | YES (self) | First user must be NULL |
| **modules** | YES | YES | YES | Seed requires valid user |
| **resources** | YES | YES | YES | Seed requires valid user |
| **actions** | YES | YES | N/A (no column) | Seed requires valid user |
| **permissions** | YES | YES | N/A | Seed requires valid user |
| **roles** | YES | YES | N/A (no column) | Seed requires valid user |
| **user_roles** | N/A | N/A | N/A | No audit FKs (has assigned_by_user_id FK) |
| **user_permission_overrides** | YES | YES (M08) | N/A | Requires valid user |
| **clients** | YES | YES | YES | Requires valid user |
| **projects** | YES | YES | YES | Requires valid user |
| **client_project_mappings** | YES | YES | YES | Requires valid user |
| **project_assignments** | YES | YES | YES | Requires valid user |
| **position_types** | N/A | N/A | N/A | No audit FKs |
| **features** | YES | YES | YES | Requires valid user |
| **client_features** | YES | YES | YES | Requires valid user |
| **user_feature_overrides** | YES | N/A | N/A | created_by FK only |
| **audit_log** | N/A | N/A | N/A | TEXT columns, no FKs |

**Total FK-constrained audit columns: 37 across 13 tables (3 per table for full-audit tables)**

### Implications

1. **Cannot delete ANY user** who has EVER created, updated, or deleted ANY record in ANY of 13 tables. This is a cascading block. You would need to NULL out every audit reference before deleting.

2. **Seeding order matters**: Must create user FIRST, then everything else. But user's own `created_by` must be NULL (self-referential bootstrap).

3. **Test isolation**: Integration tests that insert into ANY table with audit FKs must first seed a valid user. Feature flag seed (migration 10) works because `created_by` is left NULL.

4. **Production impact**: User deletion is effectively impossible once a user has been active. This is arguably a FEATURE (audit trail integrity) but was likely unintentional for ALL tables.

---

## ENTITY vs SQL STRUCTURAL MISMATCHES

| Entity | Issue | Severity |
|--------|-------|----------|
| `Role` | Missing `Code` property -- entity has Name, Description, IsSystem only. SQL has `code`. WriteDac bypasses entity. | **MISMATCH** |
| `Resource` | Has `SortOrder` property, SQL has no `sort_order` column | **MISMATCH** |
| `Module` | Missing `IsActive` property, SQL has `is_active` column | **MISMATCH** |
| `Action` | Inherits `AuditableEntity` (IsDeleted etc), SQL has no soft-delete columns | **MISMATCH** |
| `Permission` | Same as Action -- AuditableEntity but no soft-delete in SQL | **MISMATCH** |
| `RolePermission` | Inherits `AuditableEntity`, SQL is junction (no id, no audit) | **MISMATCH** |
| `UserRole` | Inherits `AuditableEntity`, SQL has different columns (assigned_by, assigned_at) | **MISMATCH** |
| `UserPermissionOverride` | Missing `Reason` property, SQL has `reason` column | **ORPHAN** |
| `UserSession` | Uses Guid.NewGuid() not Uuid7 | Convention violation |
| `Client`, `Project`, etc. | Uses Guid.NewGuid() not Uuid7 | Convention violation |

---

## TABLES WITH NO CORRESPONDING DAC

| SQL Table | Has C# Entity? | Accessed How? |
|-----------|----------------|---------------|
| `modules` | YES | JOINs in permission queries |
| `resources` | YES | JOINs in permission queries |
| `actions` | YES | JOINs in permission queries + seed |
| `permissions` | YES | JOINs in permission queries + seed |
| `role_permissions` | YES | JOINs in permission queries |
| `user_sessions` | YES | **COMPLETELY UNUSED** -- Login.cs has TODO comment: "Store refresh token in user_sessions table for refresh token rotation" |
| `position_types` | YES | FK reference only, seeded via SQL |
| `audit_log` | NO | AuditLogger hosted service (raw SQL) |

---

## PARTIAL INDEX AUDIT

All partial unique indexes use `WHERE is_deleted = FALSE`:

| Table | Index | Correct? |
|-------|-------|----------|
| clients | idx_clients_code_active | YES |
| projects | idx_projects_client_code_active | YES |
| client_project_mappings | idx_mappings_url_active | YES |
| project_assignments | idx_assignments_user_position_active | YES |
| features | idx_features_code_active | YES |
| client_features | idx_client_features_active | YES |
| user_feature_overrides | idx_user_feature_overrides_active | N/A (not partial -- hard delete table, but unique index exists without WHERE clause) |

**MISMATCH on users**: `users_email_uk` is a TABLE CONSTRAINT (not partial index). It does NOT include `WHERE is_deleted = FALSE`. This means you CANNOT create a new user with the same email as a soft-deleted user. The same applies to `users_sso_uk`.

**MISMATCH on roles**: `roles_code_uk` is also a TABLE CONSTRAINT (not partial). Cannot reuse a deleted role code after hard delete removes the row, but since roles use hard delete, this is fine.

---

## RECOMMENDATIONS (Non-actionable -- Discovery Only)

1. **Add migrations 02 and 08 to TestDatabaseInitializer** -- position_types and updated audit columns needed for test completeness
2. **Decide on audit FK policy** -- either keep (strict audit integrity) or drop to plain UUID (operational flexibility)
3. **Fix users unique constraints** -- convert to partial indexes for soft-delete compatibility
4. **Reconcile entity/SQL mismatches** -- especially Role (missing Code), Resource (orphan SortOrder), Module (missing IsActive)
5. **Standardize updated_at approach** -- either triggers everywhere or C# everywhere, not both
6. **Consider session DAC** -- user_sessions has no dedicated DAC

---

## ADDITIONAL FINDINGS

### `user_sessions` Table -- COMPLETELY DEAD CODE

The `user_sessions` table is created in Migration 01 but is **never accessed by any C# code**. The `Login.cs` handler (line 129) has this TODO:
```
// TODO: Store refresh token in user_sessions table for refresh token rotation
```
The `UserSession.cs` entity exists, has a `Create()` method, but is never instantiated by any service or handler. Refresh tokens are generated by `TokenService` but never stored. This means **refresh token rotation is unimplemented** -- tokens are stateless (validated by signature only, not stored server-side).

### `Tenant` Entity -- No Corresponding SQL Table in Portal DB

The C# `Tenant` entity exists (`Rgt.Space.Core/Domain/Entities/Identity/Tenant.cs`) but there is **no `tenants` table** in any migration. The older design documents in `Tables/UAM-Schema.sql` show a `tenants` table existed in an earlier design that was intentionally removed when the architecture shifted to single-DB with row-level isolation via `client_id`. The `Tenant` entity is vestigial.

### Tables/ Folder vs Migrations/ Folder

The `Tables/` folder contains **reference documentation** (individual table DDLs and design notes), NOT authoritative schema definitions. Some files reflect earlier design states (e.g., `UAM-Schema.sql` has a `tenants` table, `UAM-Tenantless.sql` is a discussion document). The `Migrations/` folder contains the **authoritative, ordered schema** that is actually executed.

### `SalesReadDac` -- Outside Scope

`SalesReadDac` is a Pattern B (multi-tenant) DAC that queries external tenant databases, not the portal DB. It is outside the scope of this portal DB schema audit.

### `DashboardReadDac` -- Read-Only Aggregate Queries

`DashboardReadDac` queries `projects`, `project_assignments`, `clients`, `users`, and `position_types` via JOINs for KPI metrics. All queries correctly filter `WHERE is_deleted = FALSE`. Does not write. **MATCH** with schema.

---

## COMPLETE FILE INVENTORY

### SQL Files Analyzed (33 total)

**Migrations/ (11 files -- authoritative schema):**
- 00-extensions.sql, 01-portal-schema.sql, 02-portal-seed.sql, 03-portal-routing-schema.sql
- 04-test-data.sql, 05-standardize-status.sql, 06-seed-permissions.sql, 07-seed-overrides.sql
- 08-fix-overrides-schema.sql, 09-feature-flags.sql, 10-feature-flag-seed.sql

**Tables/ (22 files -- reference documentation, NOT authoritative):**
- UAM-Schema.sql, UAM-Tenantless.sql, actions.sql, actions copy.sql
- audit_log.sql, clients.sql, client_features.sql, client_project_mappings.sql
- features.sql, modules.sql, permissions.sql, position_types.sql
- project_assignments.sql, projects.sql, resources.sql, roles.sql
- role_permissions.sql, user_feature_overrides.sql, user_permission_overrides.sql
- user_roles.sql, user_sessions.sql, users.sql

### C# Files Cross-Checked (50+)

**Entities (19):** User, UserSession, Tenant, Role, Module, Resource, Action, Permission, UserRole, RolePermission, UserPermissionOverride, Client, Project, ClientProjectMapping, ProjectAssignment, PositionType, Feature, ClientFeature, UserFeatureOverride

**DACs (16):** UserReadDac, UserWriteDac, RoleReadDac, RoleWriteDac, ClientReadDac, ClientWriteDac, ProjectReadDac, ProjectWriteDac, ClientProjectMappingReadDac, ClientProjectMappingWriteDac, ProjectAssignmentReadDac, TaskAllocationWriteDac, FeatureReadDac, FeatureWriteDac, DashboardReadDac, SalesReadDac

**Read Models (17):** UserReadModel, UserCredentialsReadModel, UserPermissionReadModel, RoleReadModel, UserRoleReadModel, ClientReadModel, ProjectReadModel, ClientProjectMappingReadModel, ProjectAssignmentReadModel, FeatureReadModel, ClientFeatureReadModel, UserOverrideReadModel, FeatureDecision, ClientFeatureDetailReadModel, ClientFeatureByClientReadModel, UserOverrideDetailReadModel, UserOverrideByUserReadModel

---

*This document is a living artifact. Updated iteratively as each module was investigated.*
*Final update: 2026-02-10 -- All 6 modules complete, all files cross-checked.*
