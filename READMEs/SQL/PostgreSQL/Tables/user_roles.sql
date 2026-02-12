-- =====================================================
-- TABLE: user_roles
-- Purpose: Junction table linking users to roles (M:N)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: Migration 12 (timestamp default for assigned_at -> UTC)
-- Business Rules:
--   - Each user-role pair must be unique (UNIQUE constraint)
--   - assigned_by_user_id tracks who granted the role
--   - assigned_at records when the role was granted
--   - No soft delete (hard delete to revoke role)
--   - No updated_at column, no trigger
--   - Uses assigned_by_user_id (not standard created_by pattern)
-- =====================================================
CREATE TABLE user_roles (
    -- Identity
    id                  UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Relationships
    user_id             UUID        NOT NULL REFERENCES users (id),
    role_id             UUID        NOT NULL REFERENCES roles (id),

    -- Assignment Audit
    assigned_by_user_id UUID        NULL REFERENCES users (id),
    assigned_at         TIMESTAMP   NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),

    -- Constraints
    CONSTRAINT user_roles_uk UNIQUE (user_id, role_id)
);

-- Indexes
-- Migration 01: Lookup by user for role resolution
CREATE INDEX idx_user_roles_user
    ON user_roles(user_id);
