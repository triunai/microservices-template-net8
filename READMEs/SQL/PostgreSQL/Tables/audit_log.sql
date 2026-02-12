-- =====================================================
-- TABLE: audit_log
-- Purpose: Local audit trail for API operations (actions, errors, request/response data)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: (none)
-- Business Rules:
--   - Append-only log (no updates, no deletes)
--   - user_id and client_id stored as TEXT (not UUID FK) for decoupling
--   - timestamp uses TIMESTAMPTZ (not TIMESTAMP) — only table that does this
--   - request_data, response_data, delta stored as BYTEA (compressed/encoded)
--   - source defaults to 'API' but can be 'SCHEDULER', 'SYSTEM', etc.
--   - idempotency_key prevents duplicate processing
--   - No soft delete, no triggers, no updated_at
-- =====================================================
CREATE TABLE audit_log (
    -- Identity
    id                UUID         PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Context
    user_id           TEXT         NULL,
    client_id         TEXT         NULL,
    ip_address        VARCHAR(50)  NULL,
    user_agent        TEXT         NULL,

    -- Action
    action            VARCHAR(100) NOT NULL,
    entity_type       VARCHAR(100) NULL,
    entity_id         VARCHAR(100) NULL,

    -- Timing
    timestamp         TIMESTAMPTZ  NOT NULL DEFAULT now(),

    -- Request
    correlation_id    VARCHAR(100) NULL,
    request_path      VARCHAR(500) NULL,

    -- Result
    is_success        BOOLEAN      NOT NULL,
    status_code       INTEGER      NULL,
    error_code        VARCHAR(50)  NULL,
    error_message     TEXT         NULL,
    duration_ms       INTEGER      NULL,

    -- Payload
    request_data      BYTEA        NULL,
    response_data     BYTEA        NULL,
    delta             BYTEA        NULL,

    -- Deduplication
    idempotency_key   VARCHAR(100) NULL,
    source            VARCHAR(50)  NOT NULL DEFAULT 'API',
    request_hash      VARCHAR(64)  NULL
);

-- Indexes
-- Migration 01: Primary query patterns for audit log retrieval
CREATE INDEX idx_audit_timestamp
    ON audit_log(timestamp DESC);

CREATE INDEX idx_audit_user_timestamp
    ON audit_log(user_id, timestamp DESC);

CREATE INDEX idx_audit_correlation
    ON audit_log(correlation_id);
