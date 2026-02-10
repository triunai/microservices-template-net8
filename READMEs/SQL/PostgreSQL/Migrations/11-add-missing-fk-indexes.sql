-- Migration: 11-add-missing-fk-indexes.sql
-- Purpose: Add indexes to FK columns lacking coverage (identified by index audit)
-- Depends on: 01-portal-schema.sql, 09-feature-flags.sql
-- Date: 2026-02-10

-- ============================================
-- CRITICAL: Permission Loading Hot Path
-- ============================================
-- These indexes support GetPermissionsAsync CTE in UserReadDac.
-- Without them, every authenticated request triggers sequential scans
-- on user_permission_overrides (for UNION/EXCEPT operations) and
-- permissions table (for resource/action JOINs).

-- Index 1: user_permission_overrides.user_id
-- Used in GetPermissionsAsync CTE for UNION (Allow) and EXCEPT (Deny)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_user_permission_overrides_user
    ON user_permission_overrides(user_id);

-- Index 2: user_permission_overrides.permission_id
-- Used in GetPermissionsAsync CTE for permission-based filtering
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_user_permission_overrides_permission
    ON user_permission_overrides(permission_id);

-- Index 3: permissions.resource_id
-- Used in GetPermissionsAsync JOIN chain: permissions → resources → modules
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_permissions_resource
    ON permissions(resource_id);

-- Index 4: permissions.action_id
-- Used in GetPermissionsAsync JOIN to actions table
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_permissions_action
    ON permissions(action_id);

-- Index 5: resources.module_id
-- Used in GetPermissionsAsync final JOIN to modules
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_resources_module
    ON resources(module_id);


-- ============================================
-- CRITICAL: Feature Flag Cascade Delete
-- ============================================
-- This index supports cascade delete in FeatureWriteDac.SoftDeleteFeatureAsync.
-- Without it, deleting a feature triggers a sequential scan on
-- user_feature_overrides to find and delete all related overrides.

-- Index 6: user_feature_overrides.feature_id
-- Used in GetUserOverridesByFeatureAsync and cascade delete CTE
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_user_feature_overrides_feature
    ON user_feature_overrides(feature_id);


-- ============================================
-- VERIFICATION QUERIES
-- ============================================

-- Test 1: Verify all indexes were created
SELECT schemaname, tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public'
  AND indexname IN (
    'idx_user_permission_overrides_user',
    'idx_user_permission_overrides_permission',
    'idx_permissions_resource',
    'idx_permissions_action',
    'idx_resources_module',
    'idx_user_feature_overrides_feature'
  )
ORDER BY tablename, indexname;

-- Test 2: Check index sizes
SELECT
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
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
ORDER BY pg_relation_size(indexrelid) DESC;

-- Test 3: Verify index usage (run after production traffic)
-- Expected: idx_scan > 0 for hot path indexes
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch
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


-- ============================================
-- ROLLBACK INSTRUCTIONS
-- ============================================
-- If indexes cause performance issues (unlikely), drop them:
/*
DROP INDEX CONCURRENTLY IF EXISTS idx_user_permission_overrides_user;
DROP INDEX CONCURRENTLY IF EXISTS idx_user_permission_overrides_permission;
DROP INDEX CONCURRENTLY IF EXISTS idx_permissions_resource;
DROP INDEX CONCURRENTLY IF EXISTS idx_permissions_action;
DROP INDEX CONCURRENTLY IF EXISTS idx_resources_module;
DROP INDEX CONCURRENTLY IF EXISTS idx_user_feature_overrides_feature;
*/


-- ============================================
-- NOTES
-- ============================================
-- 1. CONCURRENTLY keyword prevents table locks during index creation
--    Safe to run on production without downtime
--
-- 2. IF NOT EXISTS prevents errors if indexes already exist
--    (e.g., if migration is re-run)
--
-- 3. These indexes target FK columns used in JOINs and subqueries
--    Audit field FKs (created_by, updated_by) are NOT indexed
--    (low query frequency, acceptable without indexes)
--
-- 4. Expected performance impact:
--    - Permission loading: 50-90% faster (eliminates seq scans)
--    - Feature deletion: 70-95% faster (cascade delete optimization)
--    - Disk space: ~100-500KB per index (small reference tables)
--
-- 5. No application code changes required — indexes are transparent
