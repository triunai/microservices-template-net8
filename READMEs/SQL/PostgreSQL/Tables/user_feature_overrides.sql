-- =====================================================
-- TABLE: user_feature_overrides
-- Purpose: User-level feature flag refinement (FORCE_ON / FORCE_OFF)
-- Created: Migration 09 (09-feature-flags.sql)
-- Altered: Migration 11 (added idx_user_feature_overrides_feature index)
-- Business Rules:
--   - Hard delete (NO soft delete columns, NO is_deleted) -- matches user_permission_overrides pattern
--   - One override per user+feature pair (regular unique index, no zombie needed)
--   - override_state must be 'FORCE_ON' or 'FORCE_OFF'
--   - FORCE_ON cannot bypass CLIENT_OFF (enforced in FeatureGate application layer, not DB)
--   - ON DELETE CASCADE for user_id (cleanup if user is hard-deleted)
--   - ON DELETE RESTRICT for feature_id (cannot delete feature with overrides)
--   - Thin audit: only created_at and created_by (NO updated_at, NO updated_by)
--   - No trigger (no updated_at column to auto-update)
-- =====================================================
CREATE TABLE user_feature_overrides (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Relationships
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    feature_id UUID NOT NULL REFERENCES features(id) ON DELETE RESTRICT,

    -- Override Configuration
    override_state VARCHAR(20) NOT NULL
        CONSTRAINT user_feature_overrides_state_chk
        CHECK (override_state IN ('FORCE_ON', 'FORCE_OFF')),
    reason TEXT NULL,

    -- Audit (minimal -- hard delete table, no updated_at/updated_by)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id)
);

-- Indexes
-- Migration 09: One override per user+feature (hard delete removes row, no zombie needed)
CREATE UNIQUE INDEX idx_user_feature_overrides_active
    ON user_feature_overrides(user_id, feature_id);

-- Migration 09: Lookup overrides by user
CREATE INDEX idx_user_feature_overrides_user ON user_feature_overrides(user_id);

-- Migration 11: FK index for feature_id (supports cascade delete in FeatureWriteDac)
CREATE INDEX idx_user_feature_overrides_feature ON user_feature_overrides(feature_id);

-- No trigger (no updated_at column)
