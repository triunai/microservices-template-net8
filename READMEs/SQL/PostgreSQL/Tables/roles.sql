-- =====================================================
-- TABLE: roles
-- Purpose: RBAC roles that group permissions (e.g., SYS_ADMIN, PROJECT_MANAGER)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 12 (updated_at trigger, timestamp defaults -> UTC)
-- Business Rules:
--   - Role code must be globally unique (plain UNIQUE constraint)
--   - NOT soft-deletable (no is_deleted, deleted_at, deleted_by columns)
--   - is_system marks built-in roles that cannot be deleted by users
--   - is_active controls whether role can be assigned
--   - Permissions assigned via role_permissions junction table
--   - Users assigned via user_roles junction table
-- =====================================================
CREATE TABLE roles (
    -- Identity
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Attributes
    name        TEXT        NOT NULL,
    code        TEXT        NOT NULL,
    description TEXT        NULL,
    is_system   BOOLEAN     NOT NULL DEFAULT FALSE,
    is_active   BOOLEAN     NOT NULL DEFAULT TRUE,

    -- Audit
    created_at  TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by  UUID        NULL REFERENCES users (id),

    -- Constraints (no soft delete = plain UNIQUE is correct)
    CONSTRAINT roles_code_uk UNIQUE (code)
);

-- No additional indexes beyond the UNIQUE constraint.

-- Triggers (reference only -- definition in Functions/)
-- TRIGGER: trg_roles_updated_at -> update_updated_at_column() (Migration 12)
