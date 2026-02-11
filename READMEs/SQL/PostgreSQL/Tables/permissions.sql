-- =====================================================
-- TABLE: permissions
-- Purpose: RBAC permissions (resource + action combination, e.g., CLIENT_NAV_VIEW)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 11 (FK indexes on resource_id and action_id),
--          Migration 12 (updated_at trigger, timestamp defaults -> UTC)
-- Business Rules:
--   - Each permission is a unique (resource_id, action_id) pair
--   - Permission code must be globally unique
--   - NOT soft-deletable (no is_deleted, deleted_at, deleted_by columns)
--   - Permissions are assigned to roles via role_permissions junction table
--   - Permissions can be overridden per-user via user_permission_overrides
--   - Code format: MODULE.RESOURCE.ACTION (e.g., PORTAL_ROUTING.CLIENT_NAV.VIEW)
-- =====================================================
CREATE TABLE permissions (
    -- Identity
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Relationships
    resource_id UUID        NOT NULL REFERENCES resources (id),
    action_id   UUID        NOT NULL REFERENCES actions (id),

    -- Attributes
    code        TEXT        NOT NULL,       -- e.g. "CLIENT_NAV_VIEW"
    description TEXT        NULL,

    -- Audit
    created_at  TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by  UUID        NULL REFERENCES users (id),

    -- Constraints (no soft delete = plain UNIQUE is correct)
    CONSTRAINT permissions_resource_action_uk UNIQUE (resource_id, action_id),
    CONSTRAINT permissions_code_uk            UNIQUE (code)
);

-- Indexes
-- Migration 11: FK indexes for permission loading hot path (GetPermissionsAsync CTE)
CREATE INDEX idx_permissions_resource
    ON permissions(resource_id);

CREATE INDEX idx_permissions_action
    ON permissions(action_id);

-- Triggers (reference only -- definition in Functions/)
-- TRIGGER: trg_permissions_updated_at -> update_updated_at_column() (Migration 12)
