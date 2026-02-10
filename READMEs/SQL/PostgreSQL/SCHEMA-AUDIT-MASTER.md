# Schema Audit — Master Consolidation

**Date:** 2026-02-10
**Trigger:** Migration 10 (feature flag seed) exposed FK violation — all 49 integration tests crashed
**Scope:** 11 migrations, 19 entities, 16 DACs, 17 read models, 22 table reference files
**Method:** 5-agent parallel swarm + manual walkthrough

---

## Discovery Documents (Detail)

| Document | Agent | Model | Focus |
|----------|-------|-------|-------|
| [SCHEMA-DISCOVERY.md](SCHEMA-DISCOVERY.md) | Shackleton | Opus | SQL vs C# cross-check, module by module |
| [SCHEMA-VALIDATION.md](SCHEMA-VALIDATION.md) | Nansen | Opus | FK constraints, integrity, naming, idempotency |
| [SCHEMA-REVIEW.md](SCHEMA-REVIEW.md) | CodeRabbit | Opus | Anti-patterns, security, performance (29 findings) |
| [SCHEMA-INDEX-AUDIT.md](SCHEMA-INDEX-AUDIT.md) | Sonnet | Sonnet | Index coverage vs actual DAC queries |
| [SCHEMA-TEST-GAPS.md](SCHEMA-TEST-GAPS.md) | Sonnet | Sonnet | TestDatabaseInitializer gap analysis |

---

## Root Cause: The Two-Era Schema

The schema was built in two distinct design eras. Era 1 (IAM) was never retrofitted to match Era 2 patterns.

| Concern | Era 1 — Migration 01 (IAM) | Era 2 — Migrations 03, 09 (Routing + Flags) |
|---------|---------------------------|---------------------------------------------|
| Timestamps | `DEFAULT now()` | `DEFAULT (now() AT TIME ZONE 'utc')` |
| Status | `BOOLEAN is_active` | `VARCHAR(20) status CHECK(...)` |
| Uniqueness | Plain `UNIQUE` constraints | Partial indexes `WHERE is_deleted = FALSE` |
| Triggers | None (C# sets updated_at) | Auto-update `updated_at` trigger |
| Soft delete | Missing on 3 tables (actions, permissions, roles) | Consistent everywhere |

---

## Consolidated Findings — By Severity

### CRITICAL (6 findings — must fix)

| # | Finding | Source | Impact |
|---|---------|--------|--------|
| C1 | **37 FK-constrained audit columns across 13 tables** — `created_by`/`updated_by`/`deleted_by` all FK to `users(id)` with default RESTRICT. Blocks user deletion, forces seed ordering. | All 5 agents | Seed failures, test crashes, GDPR liability |
| C2 | **Migration 02 references `position_types` before it exists** — table created in migration 03 | Nansen, CodeRabbit | Production bootstrap broken if run in numeric order |
| C3 | **Migration 05 conflicts with migration 03** — tries to ADD columns that already exist, DROP columns that don't | CodeRabbit, Test Gaps | Would crash if loaded; currently masked by skip |
| C4 | **Migration 07 references `updated_by` before migration 08 adds it** — ordering bug | Nansen, Manual | Would crash if run in sequence 07→08 |
| C5 | **TestDatabaseInitializer missing 3 critical migrations** (02, 06, 08) — test schema diverges from production | Test Gaps, Nansen | Hidden bugs: UserWriteDac will crash, RBAC untestable |
| C6 | **Migration 02 seeds admin with `uuid_generate_v7()` (random)** — DevAdmin hardcoded ID `019ac92a-...` doesn't exist in any migration | Shackleton, Manual | Disconnect between C# code and SQL schema |

### HIGH (8 findings — should fix soon)

| # | Finding | Source | Impact |
|---|---------|--------|--------|
| H1 | **6 FK columns missing indexes** (permission hot path) — `user_permission_overrides.user_id/.permission_id`, `permissions.resource_id/.action_id`, `resources.module_id`, `user_feature_overrides.feature_id` | Index Audit | 50-100ms permission loading on every auth request |
| H2 | **9 tables missing `updated_at` triggers** — users, modules, resources, actions, permissions, roles, user_permission_overrides, features, client_features | All 3 Opus | Raw SQL UPDATEs won't auto-update timestamps |
| H3 | **`actions`, `permissions`, `roles` missing soft delete columns** — no `is_deleted`/`deleted_at`/`deleted_by` | CodeRabbit, Shackleton | Can't soft-delete reference data; entities expect columns that don't exist |
| H4 | **`users.email` unique constraint NOT zombie-safe** — plain UNIQUE, not partial index | Nansen, CodeRabbit | Can't recreate user with same email after soft-delete |
| H5 | **8 IAM unique constraints NOT zombie-safe** — `modules.code`, `resources.code`, `roles.code`, etc. | CodeRabbit | Same zombie problem across all IAM tables |
| H6 | **Migration 09 seeds not idempotent** — bare INSERT, no ON CONFLICT guard | CodeRabbit | Re-running migration 09 crashes on duplicate |
| H7 | **Migration 10 uses `ON CONFLICT (id)` instead of business key** — code can be duplicated if IDs differ | CodeRabbit | Seed not truly safe against partial re-runs |
| H8 | **`user_permission_overrides.user_id` uses implicit RESTRICT but `user_feature_overrides.user_id` uses CASCADE** — same semantic, different behavior | CodeRabbit | Inconsistent deletion semantics |

### Entity vs SQL Mismatches (11 found by Shackleton)

| Entity | Problem | Risk |
|--------|---------|------|
| `Role` | Missing `Code` property — WriteDac bypasses entity entirely | Medium — works, but entity is misleading |
| `Resource` | Has `SortOrder` property — SQL has no such column | Low — phantom property, never persisted |
| `Module` | Missing `IsActive` — SQL has `is_active` | Medium — can't read status |
| `Action` | Inherits `AuditableEntity` — SQL has no soft-delete columns | Low — phantom fields |
| `Permission` | Same as Action | Low |
| `RolePermission` | Inherits `AuditableEntity` — SQL is junction with no `id` | Low — entity never used for DB ops |
| `UserRole` | Inherits `AuditableEntity` — SQL has `assigned_by_user_id` instead | Low — entity never used for writes |
| `UserPermissionOverride` | Missing `Reason` property — SQL has `reason` column | Low — orphan column |
| `UserSession` | Entity + table exist, used by NOTHING | Dead code |
| `Tenant` | Entity exists, no SQL table | Vestigial from old architecture |
| 10/19 entities | Use `Guid.NewGuid()` not `Uuid7.NewUuid7()` | Convention drift |

### Dead Code

| Item | Location | Status |
|------|----------|--------|
| `user_sessions` table + `UserSession` entity | Migration 01 + Core/Entities | Never accessed. Login.cs has TODO. |
| `Tenant` entity | Core/Entities/Identity/Tenant.cs | No corresponding SQL table |
| `password_last_changed_at` column | users table (migration 01) | No C# property reads this |
| `sso_email` column | users table | Written on create, never read back |
| `Tables/actions copy.sql` | Tables/ directory | Duplicate file |
| `Tables/UAM-Tenantless.sql` | Tables/ directory | Markdown, not SQL (misnamed) |
| `Tables/UAM-Schema.sql` | Tables/ directory | Obsolete multi-tenant design |

---

## Audit Column FK Matrix

Every `REFERENCES users(id)` on audit columns, all with default RESTRICT:

| Table | created_by | updated_by | deleted_by |
|-------|:---:|:---:|:---:|
| **users** (self-ref) | FK | FK | FK |
| **modules** | FK | FK | FK |
| **resources** | FK | FK | FK |
| **actions** | FK | FK | -- |
| **permissions** | FK | FK | -- |
| **roles** | FK | FK | -- |
| **user_roles** | FK (assigned_by) | -- | -- |
| **user_permission_overrides** | FK | FK (M08) | -- |
| **clients** | FK | FK | FK |
| **projects** | FK | FK | FK |
| **client_project_mappings** | FK | FK | FK |
| **project_assignments** | FK | FK | FK |
| **features** | FK | FK | FK |
| **client_features** | FK | FK | FK |
| **user_feature_overrides** | FK | -- | -- |
| user_sessions | -- | -- | -- |
| audit_log | TEXT (no FK) | -- | -- |
| position_types | -- | -- | -- |
| role_permissions | -- | -- | -- |

**Total: 37 FK constraints on audit columns across 13 tables.**

---

## Migration Load Order Analysis

### Current TestDatabaseInitializer

```
✅ 00-extensions.sql
✅ 01-portal-schema.sql
❌ 02-portal-seed.sql          ← NEED (foundation data)
✅ 03-portal-routing-schema.sql
❌ 04-test-data.sql            ← OPTIONAL (test fixtures)
⛔ 05-standardize-status.sql   ← OBSOLETE (03 already has changes)
❌ 06-seed-permissions.sql     ← NEED (enables RBAC tests)
❌ 07-seed-overrides.sql       ← SKIP (test data, has ordering bug)
❌ 08-fix-overrides-schema.sql ← NEED (schema fix for UserWriteDac)
✅ 09-feature-flags.sql
✅ 10-feature-flag-seed.sql    ← FK BOMB (needs admin user from 02)
```

### Correct Load Order (accounting for dependency bugs)

```
00 → 01 → 03 → 02 → 06 → 08 → 09 → 10
```

**Why 03 before 02:** Migration 02 seeds `position_types`, but that table is created in 03. Must run 03 first.

### Recommended RequiredFiles Array

```csharp
private static readonly string[] RequiredFiles =
{
    "READMEs/SQL/PostgreSQL/Migrations/00-extensions.sql",
    "READMEs/SQL/PostgreSQL/Migrations/01-portal-schema.sql",
    "READMEs/SQL/PostgreSQL/Migrations/03-portal-routing-schema.sql",
    "READMEs/SQL/PostgreSQL/Migrations/02-portal-seed.sql",           // AFTER 03 (position_types FK)
    "READMEs/SQL/PostgreSQL/Migrations/06-seed-permissions.sql",      // AFTER 02 (resources + actions)
    "READMEs/SQL/PostgreSQL/Migrations/08-fix-overrides-schema.sql",  // AFTER 01 (adds columns)
    "READMEs/SQL/PostgreSQL/Migrations/09-feature-flags.sql",
    "READMEs/SQL/PostgreSQL/Migrations/10-feature-flag-seed.sql"
};
```

---

## The FK Debate: Drop vs Sentinel

### Option A: Drop FK constraints on audit columns (CodeRabbit's recommendation)
- **Pro:** Eliminates seed ordering, enables user deletion, simplifies testing
- **Pro:** App layer already validates user existence
- **Con:** 37 FKs to drop — big migration, risk of regression
- **Con:** Dapper = raw SQL, no ORM safety net. FKs are the only guard against bad audit data
- **Con:** Adding FKs back later is harder than dropping

### Option B: Keep FKs, add sentinel user (Nansen's recommendation)
- **Pro:** Preserves data integrity. One-row INSERT fixes seeding + testing
- **Pro:** Small change, low risk
- **Pro:** Can always drop FKs later if they cause operational pain
- **Con:** Still can't hard-delete users (GDPR concern)
- **Con:** Doesn't fix the conceptual problem (audit columns aren't business relationships)

### Recommendation: Option B (sentinel user) for now
- Keep FKs — they protect against bad data in a Dapper codebase
- Add a well-known SYSTEM_USER sentinel with a fixed UUID
- Use that UUID for seeding and testing
- Revisit if GDPR hard-delete is needed

---

## Fix Strategy — 3 Phases

### Phase 1: Unblock Tonight (~30 min)

1. Fix TestDatabaseInitializer load order: `00 → 01 → 03 → 02 → 06 → 08 → 09 → 10`
2. Fix migration 02 to handle position_types ordering (or rely on 03 running first)
3. Fix migration 10 seed script: use admin email lookup for created_by/updated_by
4. Run tests — expect green

### Phase 2: Retrofit Era 1 (next session, ~2 hours)

5. New migration: zombie-safe partial indexes for users.email, modules.code, etc.
6. New migration: `updated_at` triggers for all Era 1 + Era 2 tables missing them
7. New migration: 6 missing FK indexes (agent already drafted this)
8. Standardize timestamp defaults to `(now() AT TIME ZONE 'utc')`
9. Fix `ClientProjectMappingWriteDac` bare `now()` → `NOW() AT TIME ZONE 'utc'`

### Phase 3: Entity/SQL Alignment (separate effort)

10. Fix entity classes to match actual SQL columns (Role, Resource, Module, etc.)
11. Remove dead code (UserSession, Tenant, unused columns)
12. Migrate older entities from `Guid.NewGuid()` to `Uuid7.NewUuid7()`
13. Add soft-delete columns to actions, permissions, roles (if needed)

---

## Agent Agreement Matrix

| Finding | Shackleton | Nansen | CodeRabbit | Index | TestGaps | Consensus |
|---------|:---:|:---:|:---:|:---:|:---:|---|
| Audit FKs are systemic (37 across 13 tables) | YES | YES | YES | -- | YES | **UNANIMOUS** |
| Migration 02 before 03 = crash | -- | YES | -- | -- | YES | **CONFIRMED** |
| Migration 05 is obsolete | YES | YES | YES | -- | YES | **UNANIMOUS** |
| Migration 07→08 ordering bug | -- | YES | YES | -- | YES | **CONFIRMED** |
| Load migrations 02, 06, 08 | -- | YES | -- | -- | YES | **AGREED** |
| 6 missing FK indexes | -- | -- | YES | YES | -- | **AGREED** |
| Entity/SQL drift (11 instances) | YES | -- | -- | -- | -- | **SINGLE SOURCE** |
| Sentinel user (not drop FKs) | -- | YES | disagree | -- | -- | **SPLIT** |

---

*This document is the single source of truth for the schema audit. Individual discovery docs contain the detailed evidence.*
