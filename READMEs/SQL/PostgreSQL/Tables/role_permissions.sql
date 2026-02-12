-- =====================================================
-- TABLE: role_permissions
-- Purpose: Junction table linking roles to permissions (M:N)
-- Created: Migration 01 (01-portal-schema.sql)
-- Altered: (none)
-- Business Rules:
--   - Composite primary key (role_id, permission_id) prevents duplicates
--   - CASCADE DELETE on both FKs: deleting a role or permission removes the link
--   - No audit columns (junction table, not an entity)
--   - No soft delete (hard delete only)
--   - No trigger (no updated_at column)
--   - RBAC formula: EffectiveAccess = (RolePermissions UNION Override_Allow) MINUS Override_Deny
-- =====================================================
CREATE TABLE role_permissions (
    role_id       UUID NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions (id) ON DELETE CASCADE,
    CONSTRAINT role_permissions_pk PRIMARY KEY (role_id, permission_id)
);

-- Indexes
-- Migration 01: Lookup by role for permission loading
CREATE INDEX idx_role_permissions_role
    ON role_permissions(role_id);
