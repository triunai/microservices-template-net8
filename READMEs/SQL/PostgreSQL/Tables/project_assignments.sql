
-- =====================================================
-- TABLE: project_assignments
-- Purpose: Task allocation (6 positions per project)
-- Business Rules:
--   - Each position can only be assigned ONCE per project
--   - Positions must exist in position_types table
--   - Users must exist in users table
--   - Deleting a project cascades and removes its assignments
--   - Deleting a user is BLOCKED if they have assignments (data protection)
-- Design Decision:
--   - Assignments are operational data → protect via RESTRICT on user deletion
--   - Assignments are project-scoped → cascade delete on project deletion
-- =====================================================
CREATE TABLE project_assignments (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- Relationships
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    -- ON DELETE CASCADE: Project deleted → assignments deleted (expected behavior)
    
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    -- ON DELETE RESTRICT: Cannot delete user with active assignments (data protection)
    
    position_code VARCHAR(20) NOT NULL REFERENCES position_types(code),
    -- FK to position_types prevents typos like "TECH_PICS"
    
    -- Audit Trail
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),
    
    -- Soft Delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id)
    -- NOTE: Uniqueness constraints REMOVED (moved to partial indexes below for soft-delete safety)
);

-- 🛡️ HARDENING: Soft Delete-Aware Unique Indexes (THE "ZOMBIE CONSTRAINT" FIX)
-- Business Rule 1: Multiple users can hold the same position (e.g. 2 Developers),
-- BUT the same user cannot hold the same position twice.
CREATE UNIQUE INDEX idx_assignments_user_position_active 
    ON project_assignments(project_id, user_id, position_code) 
    WHERE is_deleted = FALSE;

-- Business Rule 2 (COMMENTED OUT FOR SMALL TEAMS):
-- Prevents: John being BOTH Tech PIC AND Tech Backup on same project
-- Reason: Small company may need cross-functional coverage
-- Uncomment if you want to enforce strict separation:
-- CREATE UNIQUE INDEX idx_assignments_user_active 
--     ON project_assignments(project_id, user_id) 
--     WHERE is_deleted = FALSE;

-- Performance Indexes
CREATE INDEX idx_assignments_project ON project_assignments(project_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_assignments_user ON project_assignments(user_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_assignments_position ON project_assignments(position_code) WHERE is_deleted = FALSE;

-- Documentation
COMMENT ON TABLE project_assignments IS 
'Task allocation grid. Maps 6 positions to users per project.';
