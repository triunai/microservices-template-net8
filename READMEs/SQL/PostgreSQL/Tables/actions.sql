-- =====================================================
-- TABLE: actions
-- Purpose: RBAC action types (e.g., VIEW, EDIT, DELETE, CREATE)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 12 (updated_at trigger, timestamp defaults -> UTC)
-- Business Rules:
--   - Action code must be globally unique (plain UNIQUE constraint)
--   - NOT soft-deletable (no is_deleted, deleted_at, deleted_by columns)
--   - Actions are reference data shared across all resources
--   - Combined with a resource to form a permission
-- =====================================================
CREATE TABLE actions (
    -- Identity
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Attributes
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,       -- e.g. "VIEW", "EDIT"

    -- Audit
    created_at TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID        NULL REFERENCES users (id),
    updated_at TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID        NULL REFERENCES users (id),

    -- Constraints (no soft delete = plain UNIQUE is correct)
    CONSTRAINT actions_code_uk UNIQUE (code)
);

-- No additional indexes beyond the UNIQUE constraint.

-- Triggers (reference only -- definition in Functions/)
-- TRIGGER: trg_actions_updated_at -> update_updated_at_column() (Migration 12)
