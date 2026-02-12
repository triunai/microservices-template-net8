# Seed Prompt — Schema Fix Session

You are continuing work on **Rgt.Space API** (MicroservicesBase). This session's job: **fix the SQL schema issues discovered by the 5-agent audit**.

---

## Hydration Order (read these first)

1. `READMEs/SQL/PostgreSQL/SCHEMA-AUDIT-MASTER.md` — the single source of truth, links to all 5 detail docs
2. `READMEs/State/hot-state.md` — project state, test counts, what's done
3. `MEMORY.md` (auto-loaded in system prompt)
4. `Rgt.Space.Tests/Integration/TestDatabaseInitializer.cs` — currently loads: 00, 01, 03, 09, 10

## What Happened

We wrote a seed script (migration 10) that referenced `created_by`/`updated_by` FK to `users(id)`. All 49 integration tests crashed because the DevAdmin user doesn't exist in the test DB — migration 02 (which seeds users) isn't loaded by TestDatabaseInitializer.

This triggered a full schema audit. 5 agents (3 Opus, 2 Sonnet) produced 5 discovery documents totaling ~2800 lines of findings:

- `SCHEMA-DISCOVERY.md` — SQL vs C# cross-check (37 FK audit cols, 11 entity mismatches, dead code)
- `SCHEMA-VALIDATION.md` — FK matrix, constraints, triggers, idempotency, naming
- `SCHEMA-REVIEW.md` — 29 code review findings (3 CRITICAL, 8 HIGH)
- `SCHEMA-INDEX-AUDIT.md` — 6 missing FK indexes on hot paths, 0 dead indexes
- `SCHEMA-TEST-GAPS.md` — 3 critical missing migrations, hidden bugs, recommended load order

## Current State

- **Branch:** `test/rbac-positiontype-verification-8034414594985432536`
- **Build:** 0 errors, 0 warnings
- **Tests:** FAILING — 49 integration tests crash (FK violation in migration 10)
- **97 unit tests:** Still green (no DB dependency)
- **Seed script exists:** `READMEs/SQL/PostgreSQL/Migrations/10-feature-flag-seed.sql` — has `created_by`/`updated_by` set to NULL (hack). Needs proper fix.

## The Problems (from SCHEMA-AUDIT-MASTER.md)

### CRITICAL
1. **37 FK-constrained audit columns across 13 tables** — deliberate but causes seed ordering nightmares
2. **Migration 02 references position_types before 03 creates it** — ordering bug
3. **Migration 05 conflicts with 03** — obsolete, would crash if loaded
4. **Migration 07 references updated_by before 08 adds it** — ordering bug
5. **TestDatabaseInitializer missing migrations 02, 06, 08** — test schema diverges from production
6. **DevAdmin ID hardcoded in C# doesn't exist in any migration** — `uuid_generate_v7()` generates random ID each run

### HIGH
7. **6 FK columns missing indexes** — permission loading hot path (50-100ms → 5-10ms)
8. **9 tables missing `updated_at` triggers** — Era 1 + Feature Flag tables
9. **3 tables missing soft delete columns** — actions, permissions, roles
10. **8 IAM unique constraints NOT zombie-safe** — plain UNIQUE, not partial indexes

## What To Do — 3 Phases

### Phase 1: Unblock Tests (DO THIS FIRST — ~30 min)

**Goal:** All 146 tests green again.

1. **Fix TestDatabaseInitializer load order.** The correct order accounts for migration 02 needing position_types from 03:
   ```
   00 → 01 → 03 → 02 → 06 → 08 → 09 → 10
   ```

2. **Fix migration 10 seed script.** Instead of hardcoded DevAdmin UUID or NULL, look up the seeded admin by email:
   ```sql
   INSERT INTO features (id, code, name, description, is_active, requires_client, created_by, updated_by)
   SELECT '019c4700-0001-7000-0000-000000000001', 'DASHBOARD', 'Dashboard',
          'Controls visibility of the Dashboard module', true, false,
          u.id, u.id
   FROM users u WHERE u.email = 'admin@rgtspace.com'
   UNION ALL
   -- ... repeat for other 3 features
   ```
   Keep `ON CONFLICT (id) DO NOTHING` for idempotency.

3. **Verify migration 02 works after 03.** Migration 02 inserts into `position_types` (created by 03). Since we load 03 first, the table exists. But migration 02 also inserts into `modules` (created by 01) — that's fine. Check that all INSERT statements in 02 reference tables that exist by that point.

4. **Run `dotnet build` + `dotnet test`.** Expect 146/146 green.

### Phase 2: Retrofit Era 1 (~2 hours)

Write new migration(s) to bring Era 1 tables up to Era 2 standards:

5. **Zombie-safe partial indexes** — replace plain UNIQUE constraints on:
   - `users.email`, `users.sso_provider+external_id`
   - `modules.code`, `resources.module_id+code`
   - (Leave `roles.code`, `actions.code`, `permissions.*` alone — they have no soft delete columns)

6. **Missing `updated_at` triggers** — apply `update_updated_at_column()` to:
   - `users`, `modules`, `resources`, `actions`, `permissions`, `roles`
   - `user_permission_overrides`, `features`, `client_features`

7. **6 missing FK indexes** (agent already drafted `11-add-missing-fk-indexes.sql`):
   - `user_permission_overrides.user_id` + `.permission_id` (CRITICAL — auth hot path)
   - `permissions.resource_id` + `.action_id`
   - `resources.module_id`
   - `user_feature_overrides.feature_id`

8. **Standardize timestamp defaults** — change migration 01 tables from `DEFAULT now()` to `DEFAULT (now() AT TIME ZONE 'utc')` for consistency.

9. **Fix `ClientProjectMappingWriteDac.cs`** — bare `now()` → `NOW() AT TIME ZONE 'utc'`.

### Phase 3: Entity/SQL Alignment (separate session)

10. Fix entity classes to match SQL (Role missing Code, Resource phantom SortOrder, etc.)
11. Remove dead code (UserSession, Tenant entity, unused columns)
12. Migrate `Guid.NewGuid()` → `Uuid7.NewUuid7()` in older entities

## Decision Made: Sentinel User (not drop FKs)

The FK constraints on audit columns are **deliberate and schema-wide** (37 across 13 tables). We're keeping them. The fix for seeding is:
- Migration 02 already seeds an admin user (`admin@rgtspace.com`)
- Migration 10 should reference that user by email lookup
- For the C# DevCurrentUser disconnect (hardcoded `019ac92a-...`), that's a separate issue — the ID was manually inserted in production, not via migration

## Key Gotchas

- **Load 03 BEFORE 02** — migration 02 seeds position_types, which is created in 03
- **Skip 05** — OBSOLETE, migration 03 already has the changes, loading 05 would crash
- **Skip 07** — has ordering bug (references updated_by before 08 adds it), also just test data
- **Migration 02 is not idempotent** — RETURNING fails on re-run if modules already exist. This is OK for TestDatabaseInitializer (fresh container each time) but needs fixing for production re-runs
- **VS file lock** — stop API before CLI build/test
- **Windows environment** — no bash PS1

## Files to Modify

| File | Change |
|------|--------|
| `TestDatabaseInitializer.cs` | Reorder + add migrations 02, 06, 08 |
| `10-feature-flag-seed.sql` | Use email lookup for created_by/updated_by |
| `hot-state.md` | Update after tests green |
| `MEMORY.md` | Update with schema fix learnings |

## Verification

After Phase 1:
```bash
dotnet build Rgt.Space.sln
dotnet test Rgt.Space.Tests/Rgt.Space.Tests.csproj
```
Expected: 146/146 green. If any integration tests break, check which migration is causing the issue — the load order matters.
