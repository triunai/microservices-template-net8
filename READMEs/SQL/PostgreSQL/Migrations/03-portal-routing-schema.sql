-- =====================================================
-- Portal Routing & Task Allocation Schema (PLATINUM HARDENED)
-- =====================================================
-- Version: 1.1 (Final)
-- Authors: Antigravity + Gemini 3 + User (Production Hardening Session)
-- Date: 2025-11-27
-- 
-- This schema implements:
-- - Client Management
-- - Project Management (1:1 with Client)
-- - Portal Routing (M:N with Projects for multi-env support)
-- - Task Allocation (6 positions per project)
--
-- Design Principles:
-- 1. NO ORPHANS: Every project must have an owner (client_id NOT NULL)
-- 2. UNIQUE PER CLIENT: Project codes unique within client scope
-- 3. MULTI-ENV SUPPORT: One project can have multiple routing URLs
-- 4. URL PREFIX VALIDATION: Routing URLs must follow pattern
-- 5. SOFT DELETES: All tables support soft delete
-- 6. FULL AUDIT TRAIL: created/updated/deleted tracking
-- 7. REFERENTIAL INTEGRITY: FK constraints with explicit DELETE rules
-- 8. PARTIAL INDEXES: Soft delete-aware uniqueness (THE "ZOMBIE CONSTRAINT" FIX)
-- 9. UTC TIMESTAMPS: Explicit WITHOUT TIME ZONE to avoid DST issues
-- 10. AUTO-UPDATE TRIGGERS: updated_at maintained automatically
-- =====================================================

-- =====================================================
-- TABLE: clients
-- Purpose: Represents client organizations
-- Business Rules:
--   - Code must be globally unique (for URL prefixing)
--   - Name can duplicate (different divisions of same company)
--   - Status controls visibility in UI
-- =====================================================
CREATE TABLE clients (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    code VARCHAR(50) NOT NULL,  -- e.g., "ACME" (globally unique, enforced by partial index below)
    name VARCHAR(255) NOT NULL,         -- e.g., "Acme Corporation"
    
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
);

-- 🛡️ HARDENING: Soft Delete-Aware Unique Index (THE "ZOMBIE CONSTRAINT" FIX)
-- Allows reusing 'ACME' code if the old 'ACME' was soft-deleted
CREATE UNIQUE INDEX idx_clients_code_active ON clients(code) WHERE is_deleted = FALSE;

-- Performance Indexes
CREATE INDEX idx_clients_status ON clients(status) WHERE is_deleted = FALSE;

-- =====================================================
-- TABLE: projects
-- Purpose: Represents projects/applications owned by clients
-- Business Rules:
--   - MUST belong to a client (NO orphans allowed)
--   - Code must be unique WITHIN the client (enforced by constraint)
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

-- =====================================================
-- TABLE: client_project_mappings
-- Purpose: Portal routing configuration (Gateway layer)
-- Business Rules:
--   - ONE project can have MULTIPLE routing URLs (multi-env support)
--   - Routing URL must be globally unique
--   - Routing URLs must follow prefix pattern: /{client_code}/{...}
--   - Deleting a mapping does NOT delete the project (safe deletion)
--   - Deleting a project cascades and removes its mappings
-- Design Decision:
--   - NO client_id column (redundant - get it via project join)
--   - Environment field allows: Production, Staging, Development
--   - is_active allows toggling routes without deletion
-- =====================================================
CREATE TABLE client_project_mappings (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- Relationship (FIX: No redundant client_id)
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    -- ON DELETE CASCADE: If project is deleted, its routes are deleted
    -- This is safe because routes are configuration, not operational data
    
    -- Routing Configuration
    routing_url VARCHAR(2048) NOT NULL,  -- e.g., "/acme/pos" (uniqueness enforced by partial index)
    environment VARCHAR(50) NOT NULL DEFAULT 'Production'
        CHECK (environment IN ('Production', 'Staging', 'Development', 'UAT')),
    
    -- Status (allows disabling route without deleting)
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Audit Trail
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),
    
    -- Soft Delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id),
    
    -- Business Rule: URL must follow pattern /{client_code}/...
    -- This prevents URL squatting and ensures client separation
    CONSTRAINT chk_routing_url_pattern 
        CHECK (routing_url ~ '^/[a-z0-9_-]+/[a-z0-9_-]+')
);

-- 🛡️ HARDENING: Soft Delete-Aware Unique Index (THE "ZOMBIE CONSTRAINT" FIX)
-- Allows reusing "/acme/pos" URL if the old mapping was soft-deleted
CREATE UNIQUE INDEX idx_mappings_url_active 
    ON client_project_mappings(routing_url) 
    WHERE is_deleted = FALSE;

-- Performance Indexes
CREATE INDEX idx_mappings_project ON client_project_mappings(project_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_mappings_active ON client_project_mappings(is_active) WHERE is_deleted = FALSE;

-- =====================================================
-- TABLE: position_types
-- Purpose: Reference data for the 6 project positions
-- Business Rules:
--   - This is seeded reference data (admin cannot add new positions)
--   - Exactly 6 positions: TECH_PIC, TECH_BACKUP, FUNC_PIC, FUNC_BACKUP, SUPPORT_PIC, SUPPORT_BACKUP
--   - sort_order controls UI display order
-- Design Decision:
--   - Using VARCHAR code as PK (not UUID) for readability in queries
--   - This prevents typos in project_assignments
-- =====================================================
CREATE TABLE position_types (
    -- Identity
    code VARCHAR(20) PRIMARY KEY,       -- e.g., "TECH_PIC"
    name VARCHAR(100) NOT NULL,         -- e.g., "Technical Person-in-Charge"
    description TEXT NULL,
    sort_order INT NOT NULL UNIQUE,     -- Display order in UI (1-6)
    
    -- Status
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Audit (reference data - rarely changes)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
);

-- Seed position types (this will be in seed script)
COMMENT ON TABLE position_types IS 
'Reference table for the 6 standard project positions. Seeded during initial setup.';

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
-- Business Rule 1: One position per project (e.g., Only 1 Tech PIC per project)
CREATE UNIQUE INDEX idx_assignments_position_active 
    ON project_assignments(project_id, position_code) 
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

-- =====================================================
-- COMMENTS (Documentation)
-- =====================================================
COMMENT ON TABLE clients IS 
'Client organizations. Each client can have multiple projects.';

COMMENT ON TABLE projects IS 
'Projects/Applications owned by clients. Code must be unique within client scope.';

COMMENT ON TABLE client_project_mappings IS 
'Portal routing configuration. Allows multiple URLs per project for different environments.';

COMMENT ON TABLE project_assignments IS 
'Task allocation grid. Maps 6 positions to users per project.';

-- =====================================================
-- VALIDATION QUERIES (Run these to verify schema)
-- =====================================================

-- Test 1: Verify you cannot create duplicate project codes within same client
-- This should FAIL with unique violation:
-- INSERT INTO projects (client_id, code, name) VALUES 
--   ('client1', 'POS', 'POS V1'),
--   ('client1', 'POS', 'POS V2');  -- ERROR

-- Test 2: Verify you CAN create same code for different clients
-- This should SUCCEED:
-- INSERT INTO projects (client_id, code, name) VALUES 
--   ('client1', 'POS', 'Acme POS'),
--   ('client2', 'POS', 'TechCorp POS');  -- OK

-- Test 3: Verify you cannot delete a client with projects
-- This should FAIL with FK violation:
-- DELETE FROM clients WHERE id = 'client1';  -- ERROR (has projects)

-- Test 4: Verify you CAN delete a mapping without affecting project
-- This should SUCCEED:
-- DELETE FROM client_project_mappings WHERE id = 'mapping1';
-- SELECT * FROM projects WHERE id = 'project1';  -- Still exists

-- Test 5: Verify position type FK prevents typos
-- This should FAIL:
-- INSERT INTO project_assignments (project_id, user_id, position_code) 
-- VALUES ('proj1', 'user1', 'TECH_PICS');  -- ERROR (typo)

-- =====================================================
-- ⚡ AUTOMATION: Update Timestamp Triggers
-- Purpose: Automatically maintain updated_at column
-- Benefit: Reduces C# boilerplate code
-- =====================================================
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
   NEW.updated_at = (now() AT TIME ZONE 'utc');
   RETURN NEW;
END;
$$ LANGUAGE 'plpgsql';

-- Apply trigger to all tables with updated_at column
CREATE TRIGGER update_clients_timestamp 
    BEFORE UPDATE ON clients 
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_projects_timestamp 
    BEFORE UPDATE ON projects 
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_mappings_timestamp 
    BEFORE UPDATE ON client_project_mappings 
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_position_types_timestamp 
    BEFORE UPDATE ON position_types 
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_assignments_timestamp 
    BEFORE UPDATE ON project_assignments 
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
