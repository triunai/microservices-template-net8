# TASK-012: Schema Retrofit (Era 1 → Era 2) + Auth Restore

**Priority:** HIGH
**Estimated effort:** ~2-3 hours
**Depends on:** Phase 1 fix (DONE — migration load order, DevAdmin seed, 146/146 green)
**Source:** 5-agent schema audit (`SCHEMA-AUDIT-MASTER.md`), findings H1-H5, H8

---

## Background

The schema audit revealed a "Two-Era Schema" — Era 1 (migration 01, IAM tables) was never retrofitted to match Era 2 (migrations 03/09, Routing + Flags) patterns. Phase 1 unblocked tests. This task brings Era 1 up to standard and restores auth bypasses.

## Scope

### Phase A: Auth Restore (~15 min)

Restore `AllowAnonymous()` → proper permissions on 14 feature flag endpoints. Original permissions are documented in each endpoint's TODO comment.

**Decision needed:** Keep eval endpoints (`/features/evaluate`) as `AllowAnonymous`? They're read-only, return no sensitive data, and frontends need to call them before auth is established. **Recommendation: keep eval anonymous, restore auth on the other 12.**

| Permission | Endpoints | Action |
|------------|-----------|--------|
| `GlobalEdit` | Create, Update, Delete | Restore |
| `ListView` | GetAll, GetById, GetClientFeatures, GetClientSubscriptions, GetFeatureUserOverrides, GetUserOverrides | Restore |
| `ClientEdit` | UpsertClientFeature | Restore |
| `OverrideInsert` | SetUserOverride | Restore |
| `OverrideDelete` | ClearUserOverride | Restore |
| `EvaluateView` | EvaluateFeature, BulkEvaluateFeatures | **Keep AllowAnonymous** |

Also restore `CurrentUser` in `Extensions.cs` (uncomment line 229, comment line 230).

**Files to modify (14 endpoints + 1 infra):**
```
Rgt.Space.API/Endpoints/Features/CreateFeature/Endpoint.cs
Rgt.Space.API/Endpoints/Features/UpdateFeature/Endpoint.cs
Rgt.Space.API/Endpoints/Features/DeleteFeature/Endpoint.cs
Rgt.Space.API/Endpoints/Features/GetFeatures/Endpoint.cs
Rgt.Space.API/Endpoints/Features/GetFeatureById/Endpoint.cs
Rgt.Space.API/Endpoints/Features/GetClientFeatures/Endpoint.cs
Rgt.Space.API/Endpoints/Features/GetClientSubscriptions/Endpoint.cs
Rgt.Space.API/Endpoints/Features/GetFeatureUserOverrides/Endpoint.cs
Rgt.Space.API/Endpoints/Features/GetUserOverrides/Endpoint.cs
Rgt.Space.API/Endpoints/Features/UpsertClientFeature/Endpoint.cs
Rgt.Space.API/Endpoints/Features/SetUserOverride/Endpoint.cs
Rgt.Space.API/Endpoints/Features/ClearUserOverride/Endpoint.cs
Rgt.Space.API/Endpoints/Features/EvaluateFeature/Endpoint.cs        (keep anonymous)
Rgt.Space.API/Endpoints/Features/BulkEvaluateFeatures/Endpoint.cs   (keep anonymous)
Rgt.Space.Infrastructure/Extensions.cs                               (swap DevCurrentUser → CurrentUser)
```

**WARNING:** Restoring `CurrentUser` means `ICurrentUser.Id` returns `Guid.Empty` when no JWT is present. Integration tests use `WebApplicationFactory` without JWT. The endpoint tests will need updating — either:
- Override `ICurrentUser` in `CustomWebApplicationFactory` to return DevAdmin ID, OR
- Add JWT test helpers to the test factory

This is the trickiest part of auth restore. Plan for it.

---

### Phase B: Schema Retrofit — New Migration(s)

Write **one or two** new migration files. Do NOT modify existing migrations (00-10).

#### B1: Missing FK Indexes (H1) — CRITICAL for performance

6 indexes on the permission loading hot path. Migration 11 is already drafted at `Migrations/11-add-missing-fk-indexes.sql`.

```sql
-- Auth hot path (every authenticated request)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_user_permission_overrides_user
    ON user_permission_overrides(user_id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_user_permission_overrides_permission
    ON user_permission_overrides(permission_id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_permissions_resource
    ON permissions(resource_id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_permissions_action
    ON permissions(action_id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_resources_module
    ON resources(module_id);

-- Feature flag hot path
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_user_feature_overrides_feature
    ON user_feature_overrides(feature_id);
```

**Note:** `CREATE INDEX CONCURRENTLY` cannot run inside a transaction. If using TestDatabaseInitializer (which runs each file as a single command), remove `CONCURRENTLY` for the test version, or split into individual statements.

#### B2: Zombie-Safe Partial Indexes (H4, H5)

Replace plain `UNIQUE` constraints with partial indexes `WHERE is_deleted = FALSE` on tables that HAVE soft delete columns:

```sql
-- users.email (currently plain UNIQUE)
DROP INDEX IF EXISTS users_email_key;  -- or ALTER TABLE DROP CONSTRAINT
CREATE UNIQUE INDEX idx_users_email_active ON users(email) WHERE is_deleted = FALSE;

-- users.sso_provider + external_id (if exists as constraint)
-- modules.code
-- resources.module_id + code
```

**Tables WITHOUT soft delete (actions, permissions, roles):** Leave their UNIQUE constraints as-is. They can't zombie anyway. (H3 is deferred to Phase 3.)

#### B3: Missing `updated_at` Triggers (H2)

The `update_updated_at_column()` function already exists (created in migration 03). Apply it to 9 tables:

```sql
-- Era 1 tables (migration 01)
CREATE TRIGGER trg_users_updated_at BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER trg_modules_updated_at BEFORE UPDATE ON modules
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER trg_resources_updated_at BEFORE UPDATE ON resources
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER trg_actions_updated_at BEFORE UPDATE ON actions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER trg_permissions_updated_at BEFORE UPDATE ON permissions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER trg_roles_updated_at BEFORE UPDATE ON roles
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER trg_user_permission_overrides_updated_at BEFORE UPDATE ON user_permission_overrides
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Era 2 tables missing triggers (migration 09)
CREATE TRIGGER trg_features_updated_at BEFORE UPDATE ON features
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER trg_client_features_updated_at BEFORE UPDATE ON client_features
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
```

Use `IF NOT EXISTS` or `CREATE OR REPLACE TRIGGER` (PostgreSQL 14+) for idempotency.

#### B4: Timestamp Default Standardization (H2 related)

Era 1 uses `DEFAULT now()`, Era 2 uses `DEFAULT (now() AT TIME ZONE 'utc')`. Standardize Era 1:

```sql
ALTER TABLE users ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE users ALTER COLUMN updated_at SET DEFAULT (now() AT TIME ZONE 'utc');
-- Repeat for: modules, resources, actions, permissions, roles, user_roles, user_permission_overrides
```

**Note:** This only affects NEW rows. Existing rows keep their current timestamps.

#### B5: DAC Timestamp Fix

Fix bare `now()` in `ClientProjectMappingWriteDac.cs` (4 occurrences):

| Line | Current | Fix |
|------|---------|-----|
| 39 | `now()` (x2 in INSERT) | `NOW() AT TIME ZONE 'utc'` |
| 67 | `now()` in UPDATE | `NOW() AT TIME ZONE 'utc'` |
| 95 | `now()` in soft delete | `NOW() AT TIME ZONE 'utc'` |
| 97 | `now()` in soft delete | `NOW() AT TIME ZONE 'utc'` |

Also grep other DACs for bare `now()` — this may not be the only one.

---

## Out of Scope (Phase 3)

These are documented in `SCHEMA-AUDIT-MASTER.md` but deferred:

- **H3:** Add soft delete columns to actions, permissions, roles (needs entity changes)
- **H6:** Migration 09 idempotency (don't touch existing migrations)
- **H8:** RESTRICT vs CASCADE inconsistency on user FKs (needs design decision)
- **Entity/SQL drift:** 11 mismatches (Role missing Code, Resource phantom SortOrder, etc.)
- **Dead code:** UserSession table+entity, Tenant entity, unused columns
- **Guid.NewGuid → Uuid7** migration in older entities

---

## Migration File Strategy

**Option A (recommended):** Two new migration files
- `11-add-missing-fk-indexes.sql` — indexes only (already drafted)
- `12-era1-retrofit.sql` — triggers, zombie-safe indexes, timestamp defaults

**Option B:** Single migration file
- `11-era1-retrofit.sql` — everything in one file

Recommendation: Option A. Indexes are safe and independent. Trigger/constraint changes are riskier and should be separate.

---

## Execution Plan

1. **Phase A** — Auth restore (grep-and-replace on 12 endpoints + Extensions.cs + fix test factory)
2. **Phase B1** — Validate and finalize migration 11 (FK indexes)
3. **Phase B2-B4** — Write migration 12 (triggers, zombie indexes, timestamp defaults)
4. **Phase B5** — Fix DAC bare `now()`
5. **Add migrations 11+12 to TestDatabaseInitializer**
6. **Build + test** — expect 146/146 green
7. **Update CLAUDE.md, MEMORY.md, hot-state.md**

---

## Verification

```bash
dotnet build Rgt.Space.sln
dotnet test Rgt.Space.Tests/Rgt.Space.Tests.csproj
```

Expected: 146/146 green (may gain tests if auth restore changes require new test patterns).

Post-deployment SQL verification (against real DB):
```sql
-- Verify indexes exist
SELECT indexname FROM pg_indexes WHERE tablename = 'user_permission_overrides';
SELECT indexname FROM pg_indexes WHERE tablename = 'permissions';

-- Verify triggers exist
SELECT trigger_name, event_object_table FROM information_schema.triggers
WHERE trigger_schema = 'public' ORDER BY event_object_table;

-- Verify zombie-safe indexes
SELECT indexname, indexdef FROM pg_indexes
WHERE indexdef LIKE '%WHERE is_deleted%';
```

---

## Risk Assessment

| Item | Risk | Mitigation |
|------|------|------------|
| Auth restore breaks endpoint tests | HIGH | Override ICurrentUser in test factory |
| `DROP CONSTRAINT` on users.email | MEDIUM | Verify no dependent views/functions |
| `CREATE INDEX CONCURRENTLY` in test | LOW | Remove CONCURRENTLY for test migrations |
| Trigger on high-traffic tables | LOW | BEFORE UPDATE triggers are lightweight |
| Timestamp default change | NONE | Only affects new rows |
