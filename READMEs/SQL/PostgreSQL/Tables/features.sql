-- =====================================================
-- TABLE: features
-- Purpose: Global feature flag registry with master kill switch
-- Created: Migration 09 (09-feature-flags.sql)
-- Altered: Migration 12 (added trg_features_updated_at trigger)
-- Business Rules:
--   - Code must be UPPER_SNAKE_CASE (enforced by CHECK constraint)
--   - Code unique among non-deleted rows (zombie constraint)
--   - is_active is the global kill switch (FALSE = feature off for everyone)
--   - requires_client = FALSE for system-wide features (no client subscription needed)
--   - requires_client = TRUE means client must have a client_features subscription
--   - Soft delete with full audit trail
-- =====================================================
CREATE TABLE features (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT NULL,

    -- Feature Configuration
    is_active BOOLEAN NOT NULL DEFAULT FALSE,
    requires_client BOOLEAN NOT NULL DEFAULT TRUE,

    -- Audit Trail
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),

    -- Soft Delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id),

    -- Code format validation: must be UPPER_SNAKE_CASE starting with a letter
    CONSTRAINT features_code_format_chk CHECK (code = upper(code) AND code ~ '^[A-Z][A-Z0-9_]*$')
);

-- Indexes
-- Migration 09: Zombie-safe unique constraint on code (allows code reuse after soft delete)
CREATE UNIQUE INDEX idx_features_code_active ON features(code) WHERE is_deleted = FALSE;

-- Migration 09: Quickly find active features for evaluation
CREATE INDEX idx_features_is_active ON features(is_active) WHERE is_deleted = FALSE;

-- Triggers
-- TRIGGER: trg_features_updated_at -> update_updated_at_column() (Migration 12)
CREATE OR REPLACE TRIGGER trg_features_updated_at
    BEFORE UPDATE ON features
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
