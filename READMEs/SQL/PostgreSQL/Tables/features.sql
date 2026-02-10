
-- =====================================================
-- TABLE: features
-- Purpose: Global feature flag registry and master kill switch
-- Business Rules:
--   - Code must be UPPER_SNAKE_CASE (enforced by CHECK)
--   - Code unique among non-deleted (zombie constraint)
--   - is_active is the global kill switch
--   - requires_client = FALSE for system-wide features
-- Migration: 09-feature-flags.sql
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

    CONSTRAINT features_code_format_chk CHECK (code = upper(code) AND code ~ '^[A-Z][A-Z0-9_]*$')
);

-- Zombie Constraint: code unique among non-deleted
CREATE UNIQUE INDEX idx_features_code_active ON features(code) WHERE is_deleted = FALSE;

-- Performance: quickly find active features
CREATE INDEX idx_features_is_active ON features(is_active) WHERE is_deleted = FALSE;
