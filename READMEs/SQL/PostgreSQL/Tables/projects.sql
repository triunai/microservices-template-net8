-- =====================================================
-- TABLE: projects
-- =====================================================
-- Purpose: Represents projects/applications owned by clients
-- Business Rules:
--   - MUST belong to a client (NO orphans allowed)
--   - Code must be unique WITHIN the client (enforced by partial index)
--   - External URL is the actual application URL (not routing)
--   - Deleting a client is BLOCKED if it has projects
-- Design Decision: 
--   - client_id NOT NULL prevents orphan data
--   - If you need templates, create a separate project_templates table
-- =====================================================

CREATE TABLE projects (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    code VARCHAR(50) NOT NULL,          -- e.g., "POS" (unique per client)
    name VARCHAR(255) NOT NULL,         -- e.g., "Point of Sale System"
    
    -- Ownership (FIX: No orphans allowed)
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    -- ON DELETE RESTRICT: You cannot delete a client if it has projects
    -- Must explicitly delete/reassign projects first (safety first)
    
    -- Project URLs
    external_url TEXT NULL,             -- e.g., "https://pos.acme.com" (the actual app)
    -- Note: Routing URLs are in separate table for multi-env support
    
    -- Status
    status VARCHAR(20) NOT NULL DEFAULT 'Active' 
        CHECK (status IN ('Active', 'Inactive')),
    
    -- Audit Trail
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),
    
    -- Soft Delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id)
    -- NOTE: Uniqueness constraint REMOVED (moved to partial index below for soft-delete safety)
);

-- 🛡️ HARDENING: Soft Delete-Aware Unique Index (THE "ZOMBIE CONSTRAINT" FIX)
-- Business Rule: Code must be unique within a client (but only for active projects)
-- This allows: Deleting "POS" and creating a new "POS" immediately
CREATE UNIQUE INDEX idx_projects_client_code_active 
    ON projects(client_id, code) 
    WHERE is_deleted = FALSE;

-- Performance Indexes
CREATE INDEX idx_projects_client ON projects(client_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_projects_status ON projects(status) WHERE is_deleted = FALSE;

-- Documentation
COMMENT ON TABLE projects IS 
'Projects/Applications owned by clients. Code must be unique within client scope.';
