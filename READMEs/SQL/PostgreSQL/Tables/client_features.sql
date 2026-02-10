
-- =====================================================
-- TABLE: client_features
-- Purpose: Client subscription / entitlement for a feature
-- Business Rules:
--   - One active subscription per client+feature (zombie constraint)
--   - is_enabled controls client-level toggle
--   - Soft delete (not hard delete) to preserve audit trail
--   - ON DELETE RESTRICT: cannot delete client or feature if subscriptions exist
-- Migration: 09-feature-flags.sql
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

-- Zombie Constraint: one active subscription per client+feature
CREATE UNIQUE INDEX idx_client_features_active
    ON client_features(client_id, feature_id)
    WHERE is_deleted = FALSE;

-- Performance Indexes
CREATE INDEX idx_client_features_client ON client_features(client_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_client_features_feature ON client_features(feature_id) WHERE is_deleted = FALSE;
