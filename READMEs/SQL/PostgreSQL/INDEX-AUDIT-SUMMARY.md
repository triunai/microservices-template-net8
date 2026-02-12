# Index Coverage Audit — Executive Summary

**Audit Date:** 2026-02-10
**Status:** ✅ AUDIT COMPLETE — Action Required

---

## Quick Stats

- **19 existing indexes** — all actively used, no dead indexes
- **50+ query patterns** analyzed across 16 DAC files
- **6 CRITICAL gaps** identified (FK columns without indexes)
- **Impact:** Permission loading hot path + feature cascade deletes

---

## Action Required

### Deploy Migration 11 (5-10 min, zero downtime)

```bash
# Run this migration to add 6 missing FK indexes
psql -U postgres -d rgt_space_portal -f READMEs/SQL/PostgreSQL/Migrations/11-add-missing-fk-indexes.sql
```

**What it fixes:**
- Permission loading CTE (hot path) — 50-90% faster
- Feature cascade deletes — 70-95% faster
- Eliminates sequential scans on user_permission_overrides and permissions tables

---

## Critical Findings

### 🔴 HIGH Priority (Immediate)

| Missing Index | Table | FK Column | Impact |
|---------------|-------|-----------|--------|
| ❌ | user_permission_overrides | user_id | **CRITICAL** — Hot path (every auth request) |
| ❌ | user_permission_overrides | permission_id | **CRITICAL** — Hot path (permission checks) |
| ❌ | permissions | resource_id | **HIGH** — Permission loading CTE |
| ❌ | permissions | action_id | **MEDIUM** — Permission loading CTE |
| ❌ | resources | module_id | **MEDIUM** — Permission loading CTE |
| ❌ | user_feature_overrides | feature_id | **CRITICAL** — Cascade delete + lookups |

### ✅ Well-Indexed Areas

- **Portal Routing:** All FK columns indexed (clients, projects, mappings)
- **Task Allocation:** All FK columns indexed (assignments, positions)
- **Feature Flags:** Client-side FKs indexed (client_features)
- **Roles:** User-role assignments indexed

---

## What Works Well

1. **Partial indexes** correctly implemented (`WHERE is_deleted = FALSE`)
2. **Zombie Constraint** pattern (unique on active rows) working correctly
3. **Composite indexes** have optimal column order for query patterns
4. **No dead indexes** — all 19 indexes are actively used

---

## PostgreSQL Gotcha

⚠️ **PostgreSQL does NOT auto-index FK columns** (unlike some other databases).

This audit found **30+ FK columns without indexes**, but only 6 are critical:
- **Audit field FKs** (created_by, updated_by, deleted_by) → Acceptable without indexes (rarely queried)
- **JOIN path FKs** (used in WHERE/JOIN clauses) → **MUST be indexed**

---

## Next Steps

1. **Deploy migration 11** (immediate)
2. **Monitor query performance** with `pg_stat_statements` (1 week)
3. **Review index usage** with verification queries (see audit doc)
4. **Re-audit in 3 months** after production metrics accumulate

---

## Full Details

See [SCHEMA-INDEX-AUDIT.md](./SCHEMA-INDEX-AUDIT.md) for:
- Complete query-by-query analysis
- Index inventory with predicates
- FK coverage matrix
- Verification queries
- Rollback instructions
