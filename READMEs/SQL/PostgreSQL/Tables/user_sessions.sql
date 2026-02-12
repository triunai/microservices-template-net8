-- =====================================================
-- TABLE: user_sessions
-- Purpose: Tracks user refresh token sessions for local JWT auth
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 12 (timestamp default for created_at -> UTC)
-- Business Rules:
--   - Each session has a unique refresh_token
--   - Sessions cascade-delete when the owning user is deleted
--   - Sessions can be revoked (is_revoked) without deletion
--   - replaced_by tracks token rotation chain
--   - No soft delete (sessions are hard-deleted or revoked)
--   - No updated_at column, no trigger
-- =====================================================
CREATE TABLE user_sessions (
    -- Identity
    id              UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id         UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,

    -- Token
    refresh_token   TEXT        NOT NULL,
    expires_at      TIMESTAMP   NOT NULL,

    -- Audit
    created_at      TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_ip      TEXT        NULL,
    device_info     TEXT        NULL,

    -- Revocation
    is_revoked      BOOLEAN     NOT NULL DEFAULT FALSE,
    revoked_at      TIMESTAMP   NULL,
    replaced_by     TEXT        NULL,

    -- Constraints
    CONSTRAINT user_sessions_token_uk UNIQUE (refresh_token)
);

-- No additional indexes beyond the UNIQUE constraint on refresh_token.
-- No trigger (no updated_at column).
