-- =====================================================
-- TABLE: users
-- Purpose: Local user store for the portal (SSO users synced/linked here)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 12 (zombie-safe partial indexes replacing email/sso UNIQUE,
--          updated_at trigger, timestamp defaults → UTC)
-- Business Rules:
--   - Email must be unique among non-deleted users (partial index)
--   - SSO provider + external_id must be unique among non-deleted users (partial index)
--   - Soft-deletable: is_deleted, deleted_at, deleted_by
--   - Cannot delete user if they have active project_assignments (FK RESTRICT)
--   - JIT provisioning: SSO tokens create/link local user on first login
-- =====================================================
CREATE TABLE users (
    -- Identity
    id                        UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Profile
    display_name              TEXT        NOT NULL,
    email                     TEXT        NOT NULL,
    contact_number            TEXT        NULL,
    is_active                 BOOLEAN     NOT NULL DEFAULT TRUE,

    -- Local Auth
    local_login_enabled       BOOLEAN     NOT NULL DEFAULT TRUE,
    password_hash             BYTEA       NULL,
    password_salt             BYTEA       NULL,
    password_last_changed_at  TIMESTAMP   NULL,
    password_expiry_at        TIMESTAMP   NULL,
    password_reset_token      TEXT        NULL,
    password_reset_expires_at TIMESTAMP   NULL,

    -- SSO Integration
    sso_login_enabled         BOOLEAN     NOT NULL DEFAULT FALSE,
    sso_provider              TEXT        NULL,       -- e.g., 'azuread'
    external_id               TEXT        NULL,       -- The 'sub' from the SSO provider
    sso_email                 TEXT        NULL,
    last_login_at             TIMESTAMP   NULL,
    last_login_provider       TEXT        NULL,

    -- Audit
    created_at                TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by                UUID        NULL REFERENCES users (id),
    updated_at                TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by                UUID        NULL REFERENCES users (id),

    -- Soft Delete
    is_deleted                BOOLEAN     NOT NULL DEFAULT FALSE,
    deleted_at                TIMESTAMP   NULL,
    deleted_by                UUID        NULL REFERENCES users (id)
);

-- Indexes
-- Migration 12: Zombie-safe partial index (replaces Migration 01 CONSTRAINT users_email_uk + idx_users_email)
CREATE UNIQUE INDEX idx_users_email_active
    ON users(email)
    WHERE is_deleted = FALSE;

-- Migration 12: Zombie-safe partial index (replaces Migration 01 CONSTRAINT users_sso_uk + idx_users_external_id)
CREATE UNIQUE INDEX idx_users_sso_active
    ON users(sso_provider, external_id)
    WHERE is_deleted = FALSE AND sso_provider IS NOT NULL;

-- Triggers (reference only -- definition in Functions/)
-- TRIGGER: trg_users_updated_at -> update_updated_at_column() (Migration 12)
