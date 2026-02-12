-- =====================================================
-- TABLE: client_features
-- Purpose: Client subscription / entitlement for a feature flag
-- Created: Migration 09 (09-feature-flags.sql)
-- Altered: Migration 12 (added trg_client_features_updated_at trigger)
-- Business Rules:
--   - One active subscription per client+feature pair (zombie constraint)
--   - is_enabled controls client-level toggle (independent of global is_active)
--   - Cannot delete a client that has feature subscriptions (ON DELETE RESTRICT)
--   - Cannot delete a feature that has client subscriptions (ON DELETE RESTRICT)
--   - Soft delete with full audit trail
-- =====================================================
CREATE TABLE client_features (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Relationships
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    feature_id UUID NOT NULL REFERENCES features(id) ON DELETE RESTRICT,

    -- Subscription State
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,

    -- Audit Trail
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),

    -- Soft Delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id)
);

-- Indexes
-- Migration 09: Zombie-safe unique constraint on (client_id, feature_id)
CREATE UNIQUE INDEX idx_client_features_active
    ON client_features(client_id, feature_id)
    WHERE is_deleted = FALSE;

-- Migration 09: FK index for client_id lookups (active records only)
CREATE INDEX idx_client_features_client ON client_features(client_id) WHERE is_deleted = FALSE;

-- Migration 09: FK index for feature_id lookups (active records only)
CREATE INDEX idx_client_features_feature ON client_features(feature_id) WHERE is_deleted = FALSE;

-- Triggers
-- TRIGGER: trg_client_features_updated_at -> update_updated_at_column() (Migration 12)
CREATE OR REPLACE TRIGGER trg_client_features_updated_at
    BEFORE UPDATE ON client_features
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
