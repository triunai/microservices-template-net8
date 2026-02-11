# Seed Prompt — TASK-012: Schema Retrofit + Auth Restore

You are continuing work on **Rgt.Space API** (MicroservicesBase). This session's job: **execute TASK-012 — schema retrofit (Era 1 → Era 2) and auth restore**.

---

## Hydration Order (read these first)

1. `READMEs/Tasks/TASK-012-Schema-Retrofit-Auth-Restore.md` — the full task spec with exact SQL, file paths, and execution plan
2. `READMEs/SQL/PostgreSQL/SCHEMA-AUDIT-MASTER.md` — schema audit source of truth (findings H1-H8)
3. `READMEs/State/hot-state.md` — project state, test counts, what's done
4. `MEMORY.md` (auto-loaded in system prompt)

## What Happened Before

- **TASK-009/010/011**: Feature flag system fully built — 14 endpoints, 146 tests, all green
- **Schema audit**: 5-agent swarm found "Two-Era Schema" pattern — Era 1 (IAM) never retrofitted to Era 2 standards
- **Phase 1 fix (DONE)**: Fixed migration load order, created `01a-seed-devadmin.sql`, rewrote migration 10 with email lookup. 146/146 green.
- **Phase 1 verified**: Opus code review agent cross-checked all changes — PASS with 2 non-blocking warnings

## Current State

- **Branch:** `test/rbac-positiontype-verification-8034414594985432536`
- **Build:** 0 errors, 0 warnings
- **Tests:** 146/146 green (97 unit + 49 integration)
- **Migration load order:** `00 → 01 → 03 → 01a → 02 → 06 → 08 → 09 → 10`
- **DevCurrentUser is ACTIVE** in Extensions.cs (line 230) — `CurrentUser` is commented out

## What To Do — 2 Phases

### Phase A: Auth Restore (~15 min)

1. **Restore permissions on 12 endpoints** — each has `// TODO: Restore auth` with the original permission in the comment. Grep and replace.
2. **Keep 2 eval endpoints anonymous** — `EvaluateFeature` + `BulkEvaluateFeatures` stay `AllowAnonymous`
3. **Swap DevCurrentUser → CurrentUser** in `Extensions.cs` (uncomment line 229, comment line 230)
4. **Fix test factory** — `CurrentUser` returns `Guid.Empty` without JWT. Integration endpoint tests will break. Either:
   - Override `ICurrentUser` in `CustomWebApplicationFactory` to return DevAdmin ID, OR
   - Add JWT test helpers
5. **Run tests** — expect some endpoint test failures that need fixing

### Phase B: Schema Retrofit (~2 hours)

6. **Validate migration 11** (`11-add-missing-fk-indexes.sql` — already drafted). Remove `CONCURRENTLY` for test compatibility. Add to TestDatabaseInitializer.
7. **Write migration 12** (`12-era1-retrofit.sql`):
   - Zombie-safe partial indexes for `users.email`, `modules.code`, `resources.module_id+code`
   - 9 missing `updated_at` triggers (function exists from migration 03)
   - Timestamp default standardization (`now()` → `NOW() AT TIME ZONE 'utc'`)
8. **Fix ClientProjectMappingWriteDac.cs** — 4 bare `now()` calls → `NOW() AT TIME ZONE 'utc'` (lines 39, 67, 95, 97)
9. **Grep other DACs** for bare `now()` — may not be the only one
10. **Add migrations 11+12 to TestDatabaseInitializer**
11. **Run tests** — expect 146/146 green

## Key Gotchas

- **`CREATE INDEX CONCURRENTLY`** cannot run inside a transaction — remove for TestDatabaseInitializer
- **`DROP CONSTRAINT`** on `users.email` — verify no dependent views/functions first
- **`CREATE OR REPLACE TRIGGER`** requires PostgreSQL 14+ — verify Testcontainers image version
- **VS file lock** — stop API before CLI build/test
- **Windows environment** — no bash PS1
- **Don't modify existing migrations** (00-10) — only create new ones (11, 12)

## Files to Create/Modify

| File | Action |
|------|--------|
| `Migrations/11-add-missing-fk-indexes.sql` | Validate, remove CONCURRENTLY |
| `Migrations/12-era1-retrofit.sql` | NEW — triggers, zombie indexes, timestamp defaults |
| `TestDatabaseInitializer.cs` | Add migrations 11, 12 |
| `Extensions.cs` | Swap DevCurrentUser → CurrentUser |
| 12 endpoint files | Restore `Permissions(...)` from `AllowAnonymous()` |
| `ClientProjectMappingWriteDac.cs` | Fix 4 bare `now()` calls |
| `CustomWebApplicationFactory` or test helpers | Handle no-JWT for endpoint tests |
| `hot-state.md` | Update after tests green |
| `MEMORY.md` | Update with learnings |
| `state.md` | Update auth bypass tech debt as resolved |

## Verification

```bash
dotnet build Rgt.Space.sln
dotnet test Rgt.Space.Tests/Rgt.Space.Tests.csproj
```

Expected: 146/146 green (or more if auth restore requires new test patterns).

## Swarming Opportunity

Phase A (auth) and Phase B (schema) are independent. Could deploy 2 parallel agents:
- **Agent 1**: Auth restore (12 endpoints + Extensions.cs + test factory fix)
- **Agent 2**: Schema migrations (11 validation + 12 writing + DAC fix)

But the auth agent needs to handle the test factory carefully. Consider doing auth sequentially and schema in parallel.
