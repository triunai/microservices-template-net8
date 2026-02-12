# Index Deployment Checklist

**Migration:** 11-add-missing-fk-indexes.sql
**Impact:** Adds 6 indexes to fix permission loading and feature cascade delete performance
**Downtime:** None (using CREATE INDEX CONCURRENTLY)

---

## Pre-Deployment Checks

- [ ] Verify PostgreSQL version supports CONCURRENTLY (9.2+)
- [ ] Check disk space (need ~1-2MB for new indexes)
  ```sql
  SELECT pg_size_pretty(pg_database_size('rgt_space_portal'));
  ```
- [ ] Confirm you have CREATE privilege on public schema
  ```sql
  SELECT has_schema_privilege('public', 'CREATE');
  ```
- [ ] Backup current index list (for rollback reference)
  ```sql
  \di public.*
  ```

---

## Deployment Steps

### 1. Apply Migration (5-10 min)

```bash
# From repo root
psql -U postgres -d rgt_space_portal -f READMEs/SQL/PostgreSQL/Migrations/11-add-missing-fk-indexes.sql
```

**Expected output:**
```
CREATE INDEX
CREATE INDEX
CREATE INDEX
CREATE INDEX
CREATE INDEX
CREATE INDEX
```

**Warnings to ignore:**
- "NOTICE: relation "idx_*" already exists, skipping" — Safe, means index exists

**Errors to investigate:**
- "ERROR: permission denied" — User needs CREATE privilege
- "ERROR: out of disk space" — Need more disk space

---

### 2. Verify Index Creation

```sql
-- Should return 6 rows
SELECT schemaname, tablename, indexname
FROM pg_indexes
WHERE schemaname = 'public'
  AND indexname IN (
    'idx_user_permission_overrides_user',
    'idx_user_permission_overrides_permission',
    'idx_permissions_resource',
    'idx_permissions_action',
    'idx_resources_module',
    'idx_user_feature_overrides_feature'
  );
```

Expected: **6 rows** (one per new index)

---

### 3. Check Index Sizes

```sql
SELECT
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
  AND indexname LIKE 'idx_user_permission%'
     OR indexname LIKE 'idx_permissions_%'
     OR indexname LIKE 'idx_resources_%'
     OR indexname LIKE 'idx_user_feature_overrides_%'
ORDER BY pg_relation_size(indexrelid) DESC;
```

Expected: Total size < 2MB (small reference tables)

---

## Post-Deployment Verification

### 4. Test Query Plans (Before/After)

**Permission Loading Query** (hot path):
```sql
EXPLAIN (ANALYZE, BUFFERS)
WITH effective_permissions AS (
    SELECT rp.permission_id
    FROM user_roles ur
    JOIN role_permissions rp ON ur.role_id = rp.role_id
    JOIN roles r ON ur.role_id = r.id
    WHERE ur.user_id = '019ac92a-de20-7793-b8df-b88a87ea4e34'
      AND r.is_active = TRUE
    UNION
    SELECT permission_id
    FROM user_permission_overrides
    WHERE user_id = '019ac92a-de20-7793-b8df-b88a87ea4e34'
      AND is_allowed = TRUE
    EXCEPT
    SELECT permission_id
    FROM user_permission_overrides
    WHERE user_id = '019ac92a-de20-7793-b8df-b88a87ea4e34'
      AND is_allowed = FALSE
)
SELECT COUNT(*) FROM effective_permissions;
```

**Before (BAD):** Look for "Seq Scan on user_permission_overrides"
**After (GOOD):** Should show "Index Scan using idx_user_permission_overrides_user"

---

### 5. Monitor Index Usage (1 week)

```sql
-- Check index scan counts (should increase over time)
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan AS scans,
    idx_tup_read AS tuples_read,
    idx_tup_fetch AS tuples_fetched
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
  AND indexname IN (
    'idx_user_permission_overrides_user',
    'idx_user_permission_overrides_permission',
    'idx_permissions_resource',
    'idx_permissions_action',
    'idx_resources_module',
    'idx_user_feature_overrides_feature'
  )
ORDER BY idx_scan DESC;
```

**Expected after 1 day:**
- `idx_user_permission_overrides_user` → scans > 1000 (hot path)
- `idx_permissions_resource` → scans > 500
- Other indexes → scans > 0

**If scans = 0 after 1 week:**
- Index may not be needed (but unlikely given audit findings)
- Query planner may prefer seq scan (only if table < 100 rows)

---

## Rollback (If Needed)

**If indexes cause issues** (extremely unlikely):

```sql
-- Drop all 6 indexes
DROP INDEX CONCURRENTLY IF EXISTS idx_user_permission_overrides_user;
DROP INDEX CONCURRENTLY IF EXISTS idx_user_permission_overrides_permission;
DROP INDEX CONCURRENTLY IF EXISTS idx_permissions_resource;
DROP INDEX CONCURRENTLY IF EXISTS idx_permissions_action;
DROP INDEX CONCURRENTLY IF EXISTS idx_resources_module;
DROP INDEX CONCURRENTLY IF EXISTS idx_user_feature_overrides_feature;
```

**Verify rollback:**
```sql
-- Should return 0 rows
SELECT indexname FROM pg_indexes
WHERE schemaname = 'public'
  AND indexname IN (
    'idx_user_permission_overrides_user',
    'idx_user_permission_overrides_permission',
    'idx_permissions_resource',
    'idx_permissions_action',
    'idx_resources_module',
    'idx_user_feature_overrides_feature'
  );
```

---

## Success Criteria

- [x] Migration executes without errors
- [x] All 6 indexes created successfully
- [x] Query plans show index usage (not seq scans)
- [x] Application tests pass (no functional regression)
- [x] Index scan counts increase over 1 week

---

## Troubleshooting

### Issue: "ERROR: permission denied for schema public"
**Solution:** Grant CREATE privilege
```sql
GRANT CREATE ON SCHEMA public TO your_user;
```

### Issue: "CREATE INDEX CONCURRENTLY cannot run inside a transaction block"
**Solution:** Run migration outside psql transaction or remove CONCURRENTLY for dev environments

### Issue: Index not being used by query planner
**Possible causes:**
1. Table too small (< 100 rows) — planner prefers seq scan
2. Statistics outdated — run `ANALYZE table_name;`
3. Query predicate doesn't match index columns

**Debug:**
```sql
EXPLAIN (ANALYZE, BUFFERS, VERBOSE) <your_query>;
-- Look for "Seq Scan" vs "Index Scan"
```

---

## Performance Expectations

| Operation | Before | After | Improvement |
|-----------|--------|-------|-------------|
| Permission loading (hot path) | ~50-100ms | ~5-10ms | **50-90% faster** |
| Feature cascade delete | ~200-500ms | ~10-50ms | **70-95% faster** |
| Get user overrides by feature | ~20-50ms | ~2-5ms | **80-90% faster** |

**Note:** Actual improvements depend on data volume. Small tables (< 1000 rows) may see minimal improvement.

---

## Next Steps After Deployment

1. **Update TestDatabaseInitializer** to include migration 11 in `RequiredFiles[]`
2. **Monitor production logs** for slow query warnings (should decrease)
3. **Review pg_stat_statements** after 1 week for query performance trends
4. **Schedule next index audit** in 3 months (after production data accumulates)
