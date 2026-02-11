-- Migration: 11-add-missing-fk-indexes.sql
-- Purpose: Add indexes to FK columns lacking coverage (identified by schema audit)
-- Depends on: 01-portal-schema.sql, 09-feature-flags.sql
-- Date: 2026-02-10
-- NOTE: CONCURRENTLY removed for TestDatabaseInitializer compatibility.
--       For production on live tables, run each CREATE INDEX with CONCURRENTLY manually.

-- ============================================
-- CRITICAL: Permission Loading Hot Path
-- ============================================
-- These indexes support GetPermissionsAsync CTE in UserReadDac.
-- Without them, every authenticated request triggers sequential scans
-- on user_permission_overrides and permissions tables.

CREATE INDEX IF NOT EXISTS idx_user_permission_overrides_user
    ON user_permission_overrides(user_id);

CREATE INDEX IF NOT EXISTS idx_user_permission_overrides_permission
    ON user_permission_overrides(permission_id);

CREATE INDEX IF NOT EXISTS idx_permissions_resource
    ON permissions(resource_id);

CREATE INDEX IF NOT EXISTS idx_permissions_action
    ON permissions(action_id);

CREATE INDEX IF NOT EXISTS idx_resources_module
    ON resources(module_id);

-- ============================================
-- Feature Flag Hot Path
-- ============================================
-- Supports cascade delete in FeatureWriteDac and user override lookups.

CREATE INDEX IF NOT EXISTS idx_user_feature_overrides_feature
    ON user_feature_overrides(feature_id);
