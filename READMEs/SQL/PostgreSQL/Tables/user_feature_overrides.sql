
-- =====================================================
-- TABLE: user_feature_overrides
-- Purpose: User-level feature flag refinement (FORCE_ON / FORCE_OFF)
-- Business Rules:
--   - Hard delete (matches user_permission_overrides pattern)
--   - One override per user+feature (regular unique index, no zombie needed)
--   - override_state must be FORCE_ON or FORCE_OFF
--   - FORCE_ON cannot bypass CLIENT_OFF (enforced in FeatureGate, not DB)
--   - ON DELETE CASCADE for user_id (cleanup if user hard-deleted)
-- Migration: 09-feature-flags.sql
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

    -- Audit (minimal — hard delete table)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id)
);

-- One override per user+feature (hard delete removes row, no zombie needed)
CREATE UNIQUE INDEX idx_user_feature_overrides_active
    ON user_feature_overrides(user_id, feature_id);

-- Performance: lookup overrides by user
CREATE INDEX idx_user_feature_overrides_user ON user_feature_overrides(user_id);
