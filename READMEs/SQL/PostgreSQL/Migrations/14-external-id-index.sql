-- =====================================================
-- Migration 14: External ID Index Change
-- Purpose: Remove sso_provider from external_id lookup index
--          to prevent IdP flip-flop when users switch providers
-- TASK-015 Fix 9
-- =====================================================

-- Drop old compound index (sso_provider + external_id)
DROP INDEX IF EXISTS idx_users_sso_active;

-- Create new index on external_id alone
-- Broker's `sub` is IdP-agnostic (stable UUID), so provider
-- should not be part of the uniqueness constraint
CREATE UNIQUE INDEX idx_users_sso_active
    ON users(external_id)
    WHERE is_deleted = FALSE AND external_id IS NOT NULL;
