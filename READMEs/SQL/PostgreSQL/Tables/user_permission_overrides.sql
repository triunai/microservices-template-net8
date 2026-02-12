-- =====================================================
-- TABLE: user_permission_overrides
-- Purpose: Per-user permission grants/denials that override role-based access
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 08 (added updated_at, updated_by columns),
--          Migration 11 (FK indexes on user_id, permission_id),
--          Migration 12 (updated_at trigger, timestamp defaults -> UTC)
-- Business Rules:
--   - Each user-permission pair must be unique (UNIQUE constraint)
--   - is_allowed = TRUE grants the permission (override allow)
--   - is_allowed = FALSE denies the permission (override deny)
--   - RBAC formula: EffectiveAccess = (RolePermissions UNION Override_Allow) MINUS Override_Deny
--   - reason documents why the override was created
--   - No soft delete (hard delete to remove override)
-- =====================================================
CREATE TABLE user_permission_overrides (
    -- Identity
    id            UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Relationships
    user_id       UUID        NOT NULL REFERENCES users (id),
    permission_id UUID        NOT NULL REFERENCES permissions (id),

    -- Override
    is_allowed    BOOLEAN     NOT NULL,     -- TRUE = Grant, FALSE = Deny
    reason        TEXT        NULL,

    -- Audit
    created_at    TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by    UUID        NULL REFERENCES users (id),
    updated_at    TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),   -- Added by Migration 08
    updated_by    UUID        NULL REFERENCES users (id),                     -- Added by Migration 08

    -- Constraints
    CONSTRAINT user_permission_overrides_uk UNIQUE (user_id, permission_id)
);

-- Indexes
-- Migration 11: FK indexes for permission loading hot path (GetPermissionsAsync CTE)
CREATE INDEX idx_user_permission_overrides_user
    ON user_permission_overrides(user_id);

CREATE INDEX idx_user_permission_overrides_permission
    ON user_permission_overrides(permission_id);

-- Triggers (reference only -- definition in Functions/)
-- TRIGGER: trg_user_permission_overrides_updated_at -> update_updated_at_column() (Migration 12)
