-- =====================================================
-- TABLE: project_assignments
-- Purpose: Task allocation grid mapping users to the 6 staffing positions per project
-- Created: Migration 03 (03-portal-routing-schema.sql)
-- Business Rules:
--   - Same user cannot hold the same position twice on the same project (among non-deleted)
--   - Same user CAN hold multiple different positions on the same project (small team flexibility)
--   - position_code FK to position_types prevents typos (e.g., "TECH_PICS")
--   - Deleting a project CASCADE deletes its assignments
--   - Deleting a user is BLOCKED if they have assignments (ON DELETE RESTRICT)
--   - Soft delete with full audit trail
-- =====================================================
CREATE TABLE project_assignments (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Relationships
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    position_code VARCHAR(20) NOT NULL REFERENCES position_types(code),

    -- Audit Trail
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),

    -- Soft Delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id)
);

-- Indexes
-- Migration 03: Zombie-safe unique constraint on (project_id, user_id, position_code)
-- Prevents same user from holding the same position twice on a project
CREATE UNIQUE INDEX idx_assignments_user_position_active
    ON project_assignments(project_id, user_id, position_code)
    WHERE is_deleted = FALSE;

-- Migration 03: FK index for project_id lookups (active records only)
CREATE INDEX idx_assignments_project ON project_assignments(project_id) WHERE is_deleted = FALSE;

-- Migration 03: FK index for user_id lookups (active records only)
CREATE INDEX idx_assignments_user ON project_assignments(user_id) WHERE is_deleted = FALSE;

-- Migration 03: FK index for position_code lookups (active records only)
CREATE INDEX idx_assignments_position ON project_assignments(position_code) WHERE is_deleted = FALSE;

-- Triggers
-- TRIGGER: update_assignments_timestamp -> update_updated_at_column() (Migration 03)
CREATE TRIGGER update_assignments_timestamp
    BEFORE UPDATE ON project_assignments
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
