-- =====================================================
-- TABLE: modules
-- Purpose: Top-level RBAC grouping (e.g., PORTAL_ROUTING, IDENTITY, TASK_ALLOCATION)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 12 (zombie-safe partial index replacing code UNIQUE,
--          updated_at trigger, timestamp defaults -> UTC)
-- Business Rules:
--   - Module code must be unique among non-deleted modules (partial index)
--   - Soft-deletable: is_deleted, deleted_at, deleted_by
--   - sort_order controls UI display ordering
--   - Each module contains multiple resources
-- =====================================================
CREATE TABLE modules (
    -- Identity
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Attributes
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,       -- e.g. "PORTAL_ROUTING"
    is_active  BOOLEAN     NOT NULL DEFAULT TRUE,
    sort_order INT         NULL,

    -- Audit
    created_at TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID        NULL REFERENCES users (id),
    updated_at TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID        NULL REFERENCES users (id),

    -- Soft Delete
    is_deleted BOOLEAN     NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP   NULL,
    deleted_by UUID        NULL REFERENCES users (id)
);

-- Indexes
-- Migration 12: Zombie-safe partial index (replaces Migration 01 CONSTRAINT modules_code_uk)
CREATE UNIQUE INDEX idx_modules_code_active
    ON modules(code)
    WHERE is_deleted = FALSE;

-- Triggers (reference only -- definition in Functions/)
-- TRIGGER: trg_modules_updated_at -> update_updated_at_column() (Migration 12)
