-- =====================================================
-- TABLE: resources
-- Purpose: RBAC resources within a module (e.g., CLIENT_NAV, MEMBERS_DIST)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 11 (FK index on module_id),
--          Migration 12 (zombie-safe partial index replacing module_code UNIQUE,
--          updated_at trigger, timestamp defaults -> UTC)
-- Business Rules:
--   - Resource code must be unique within its module among non-deleted resources
--   - Soft-deletable: is_deleted, deleted_at, deleted_by
--   - Each resource belongs to exactly one module
--   - Each resource can have multiple permissions (resource + action)
-- =====================================================
CREATE TABLE resources (
    -- Identity
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Relationships
    module_id  UUID        NOT NULL REFERENCES modules (id),

    -- Attributes
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,       -- e.g. "CLIENT_NAV"

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
-- Migration 11: FK index for join performance
CREATE INDEX idx_resources_module
    ON resources(module_id);

-- Migration 12: Zombie-safe partial index (replaces Migration 01 CONSTRAINT resources_module_code_uk)
CREATE UNIQUE INDEX idx_resources_module_code_active
    ON resources(module_id, code)
    WHERE is_deleted = FALSE;

-- Triggers (reference only -- definition in Functions/)
-- TRIGGER: trg_resources_updated_at -> update_updated_at_column() (Migration 12)
