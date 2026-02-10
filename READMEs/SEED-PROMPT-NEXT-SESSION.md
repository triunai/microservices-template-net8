# Seed Prompt — Next Session: Seed Script + SQL Full Audit

Copy everything below this line and paste as your first message in a new Claude Code session.

---

## Hydration

Read these 2 files first:

1. `READMEs/State/hot-state.md` — full project state, test counts (146 all green), what's done
2. `MEMORY.md` (auto-loaded) — gotchas, patterns, conventions

Then skim the SQL scope:

3. `READMEs/SQL/PostgreSQL/Migrations/09-feature-flags.sql` — latest migration (feature flag tables)
4. `Rgt.Space.Tests/Integration/Fixtures/TestDatabaseInitializer.cs` — where to add migration 10

## What You're Doing — 3 Tasks

### Task 1: Feature Flag Seed Script (do first, sequential, ~5 min)

Create `READMEs/SQL/PostgreSQL/Migrations/10-feature-flag-seed.sql`:

```sql
-- Migration 10: Seed module-level feature flags
-- These are the 4 core module flags used by frontend evaluation.
-- All system-wide (requires_client=false) — module visibility is not client-dependent.

INSERT INTO features (id, code, name, description, is_active, requires_client, created_by, updated_by)
VALUES
  ('019c4700-0001-7000-0000-000000000001', 'DASHBOARD', 'Dashboard', 'Controls visibility of the Dashboard module', true, false, '019ac92a-de20-7793-b8df-b88a87ea4e34', '019ac92a-de20-7793-b8df-b88a87ea4e34'),
  ('019c4700-0002-7000-0000-000000000002', 'TASK_ALLOCATION', 'Task Allocation', 'Controls visibility of the Task Allocation module', true, false, '019ac92a-de20-7793-b8df-b88a87ea4e34', '019ac92a-de20-7793-b8df-b88a87ea4e34'),
  ('019c4700-0003-7000-0000-000000000003', 'PORTAL_ROUTING', 'Portal Routing', 'Controls visibility of the Portal Routing module', true, false, '019ac92a-de20-7793-b8df-b88a87ea4e34', '019ac92a-de20-7793-b8df-b88a87ea4e34'),
  ('019c4700-0004-7000-0000-000000000004', 'USER_MANAGEMENT', 'User Management', 'Controls visibility of the User Management module', true, false, '019ac92a-de20-7793-b8df-b88a87ea4e34', '019ac92a-de20-7793-b8df-b88a87ea4e34')
ON CONFLICT (id) DO NOTHING;
```

Notes:
- `created_by`/`updated_by` = DevAdmin ID `019ac92a-de20-7793-b8df-b88a87ea4e34`
- Use `ON CONFLICT (id) DO NOTHING` (not bare form)
- All `requires_client=false` — module visibility is system-wide, not per-client
- All `is_active=true` — features start enabled, admin can disable via UI

Then add `"READMEs/SQL/PostgreSQL/Migrations/10-feature-flag-seed.sql"` to `TestDatabaseInitializer.RequiredFiles[]`.

Build + run tests to verify. Should still be 146 all green.

### Task 2: SQL Schema Full Audit (parallel agents, ~15 min)

Deploy 2 agents IN PARALLEL immediately after Task 1:

**Agent A — Shackleton Research: Module-by-Module Cross-Check**

Prompt: "Walk through every SQL file in `READMEs/SQL/PostgreSQL/` (both Migrations/ and Tables/ directories — 30+ files). For each module, cross-check the SQL schema against the live C# codebase. Check: (1) every table column has a matching DAC query or read model field, (2) FKs match entity relationships, (3) indexes cover the queries DACs actually run, (4) `is_deleted` filters present where needed, (5) partial unique indexes match convention, (6) orphaned columns/tables not used by any DAC. Work module by module: Identity → Portal Routing → Task Allocation → Feature Flags → Audit → Cross-cutting (triggers, extensions, functions). Write findings iteratively to `READMEs/SQL/PostgreSQL/SCHEMA-DISCOVERY.md`. Tag findings as MATCH/MISMATCH/ORPHAN/MISSING."

**Agent B — Nansen Verification: Schema Integrity Validation**

Prompt: "Validate the PostgreSQL schema integrity across all migrations (00-09) and table definitions (30+ files) in `READMEs/SQL/PostgreSQL/`. Check: (1) every table has proper PKs, (2) all FKs reference valid tables with correct ON DELETE behavior, (3) NOT NULL constraints are appropriate, (4) CHECK constraints are present where needed, (5) defaults make sense, (6) trigger functions exist and are correct, (7) migrations are idempotent and ordered correctly, (8) naming is consistent (snake_case, plural tables), (9) seed data FKs reference valid rows, (10) `TestDatabaseInitializer` loads all necessary migrations. Write findings to `READMEs/SQL/PostgreSQL/SCHEMA-VALIDATION.md`. Tag findings as CRITICAL/WARNING/NOTE."

Both agents write to SEPARATE files. No shared writes.

### Task 3: After Audit Complete

Review both discovery docs. Any CRITICAL findings → fix immediately or log to tech debt. Then proceed to auth restore if time permits.

## Current State Snapshot
- Branch: `test/rbac-positiontype-verification-8034414594985432536`
- Build: 0 errors, 0 warnings
- Tests: 97 unit + 49 integration = 146 total, ALL GREEN
- TASK-009 (CRUD): COMPLETE
- TASK-010 (reads): COMPLETE
- TASK-011 (evaluation): COMPLETE — all prod code + all tests done
- 14 feature flag endpoints with `AllowAnonymous` (auth restore pending)
- Eval endpoints may stay `AllowAnonymous` permanently (read-only, no sensitive data)
