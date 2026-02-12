-- Migration: 09-feature-flags.sql
-- Feature Flag System (TASK-009)
-- Creates: features, client_features, user_feature_overrides
-- Depends on: 00-extensions.sql (uuid_generate_v7), 01-portal-schema.sql (users, clients)

-- ============================================
-- Table: features (Global Registry + Kill Switch)
-- ============================================
CREATE TABLE features (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT NULL,

    is_active BOOLEAN NOT NULL DEFAULT FALSE,
    requires_client BOOLEAN NOT NULL DEFAULT TRUE,

    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),

    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id),

    CONSTRAINT features_code_format_chk CHECK (code = upper(code) AND code ~ '^[A-Z][A-Z0-9_]*$')
);

-- Zombie Constraint: code unique among non-deleted
CREATE UNIQUE INDEX idx_features_code_active ON features(code) WHERE is_deleted = FALSE;

-- Performance: quickly find active features
CREATE INDEX idx_features_is_active ON features(is_active) WHERE is_deleted = FALSE;


-- ============================================
-- Table: client_features (Client Subscription / Entitlement)
-- ============================================
CREATE TABLE client_features (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),

    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    feature_id UUID NOT NULL REFERENCES features(id) ON DELETE RESTRICT,

    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,

    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),

    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id)
);

-- Zombie Constraint: one active subscription per client+feature
CREATE UNIQUE INDEX idx_client_features_active
    ON client_features(client_id, feature_id)
    WHERE is_deleted = FALSE;

-- Performance: lookup by client, lookup by feature
CREATE INDEX idx_client_features_client ON client_features(client_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_client_features_feature ON client_features(feature_id) WHERE is_deleted = FALSE;


-- ============================================
-- Table: user_feature_overrides (User-level Refinement)
-- Hard delete — matches user_permission_overrides pattern
-- ============================================
CREATE TABLE user_feature_overrides (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),

    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    feature_id UUID NOT NULL REFERENCES features(id) ON DELETE RESTRICT,

    override_state VARCHAR(20) NOT NULL
        CONSTRAINT user_feature_overrides_state_chk
        CHECK (override_state IN ('FORCE_ON', 'FORCE_OFF')),
    reason TEXT NULL,

    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id)
);

-- One override per user+feature (hard delete removes row)
CREATE UNIQUE INDEX idx_user_feature_overrides_active
    ON user_feature_overrides(user_id, feature_id);

-- Performance: lookup overrides by user
CREATE INDEX idx_user_feature_overrides_user ON user_feature_overrides(user_id);


-- ============================================
-- Seed: Test feature flag (inactive by default for safe rollout)
-- ============================================
INSERT INTO features (code, name, description, is_active, requires_client)
VALUES ('DASHBOARD_V2', 'Dashboard V2', 'New dashboard experience', FALSE, TRUE);

INSERT INTO features (code, name, description, is_active, requires_client)
VALUES ('SYS_COMBO_BREAK_DEBUG', 'Combo Break Debugger', 'System-level debug feature for combo-break endpoints', FALSE, FALSE);
