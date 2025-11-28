-- =====================================================
-- Portal Routing & Task Allocation Schema (PLATINUM HARDENED)
-- =====================================================
-- Version: 1.1 (Final Release)
-- Status: Production Ready
-- Logic: Dedicated Instances (No Shared Projects) + Safe Deletes
-- =====================================================

-- 0. PREREQUISITES
-- Ensure UUIDv7 function exists
-- CREATE EXTENSION IF NOT EXISTS "pgcrypto";
-- (Assuming uuid_generate_v7 function is already defined in Module 0)

-- =====================================================
-- 1. TABLE: clients
-- =====================================================
CREATE TABLE clients (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- Identity
    -- NOTE: Uniqueness enforced by Partial Index below (not here)
    code            VARCHAR(50) NOT NULL, 
    name            VARCHAR(255) NOT NULL,
    
    -- Status
    status          VARCHAR(20) NOT NULL DEFAULT 'Active' 
                    CHECK (status IN ('Active', 'Inactive')),
    
    -- Audit & Soft Delete
    created_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by      UUID, -- References users(id)
    updated_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by      UUID,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMP WITHOUT TIME ZONE,
    deleted_by      UUID
);

-- 🛡️ HARDENING: The "Zombie Constraint" Fix
-- Allows reusing 'ACME' code immediately after the old 'ACME' is soft-deleted.
CREATE UNIQUE INDEX idx_clients_code_active ON clients (code) WHERE is_deleted = FALSE;

-- Performance
CREATE INDEX idx_clients_status ON clients(status) WHERE is_deleted = FALSE;

-- =====================================================
-- 2. TABLE: projects
-- =====================================================
CREATE TABLE projects (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- Identity
    code            VARCHAR(50) NOT NULL, -- e.g. "POS"
    name            VARCHAR(255) NOT NULL,
    
    -- 🔗 Ownership (NO ORPHANS)
    -- We enforce NOT NULL to prevent "Floating" projects. 
    -- If you need templates, use a separate 'project_templates' table.
    client_id       UUID NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    
    -- Metadata
    external_url    TEXT, -- The destination app URL (e.g. https://app.acme.com)
    status          VARCHAR(20) NOT NULL DEFAULT 'Active' 
                    CHECK (status IN ('Active', 'Inactive')),
    
    -- Audit & Soft Delete
    created_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by      UUID,
    updated_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by      UUID,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMP WITHOUT TIME ZONE,
    deleted_by      UUID
);

-- 🛡️ HARDENING: Unique Project Code PER CLIENT (Ignoring Deleted)
-- "Acme" can have "POS". "TechCorp" can have "POS".
-- "Acme" CANNOT have two active "POS" projects.
CREATE UNIQUE INDEX idx_projects_client_code_active 
    ON projects (client_id, code) 
    WHERE is_deleted = FALSE;

CREATE INDEX idx_projects_client ON projects(client_id);

-- =====================================================
-- 3. TABLE: client_project_mappings
-- =====================================================
CREATE TABLE client_project_mappings (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- 🔗 Relationship (Project owns the route)
    -- If Project is deleted, the route is deleted (Cascade).
    project_id      UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    
    -- Routing
    -- NOTE: Uniqueness enforced by Partial Index below
    routing_url     VARCHAR(2048) NOT NULL,
    environment     VARCHAR(50) NOT NULL DEFAULT 'Production' 
                    CHECK (environment IN ('Production', 'Staging', 'Development', 'UAT')),
    
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Audit & Soft Delete
    created_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by      UUID,
    updated_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by      UUID,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMP WITHOUT TIME ZONE,
    deleted_by      UUID,

    -- 🛡️ HARDENING: URL Pattern Validation
    -- Must start with slash, followed by client segment, followed by project segment
    CONSTRAINT chk_routing_url_pattern CHECK (routing_url ~ '^/[a-z0-9_-]+/[a-z0-9_-]+')
);

-- 🛡️ HARDENING: Unique URL (Ignoring Deleted)
-- Prevents collisions on active routes only.
CREATE UNIQUE INDEX idx_mappings_url_active 
    ON client_project_mappings (routing_url) 
    WHERE is_deleted = FALSE;

CREATE INDEX idx_mappings_project ON client_project_mappings(project_id);

-- =====================================================
-- 4. TABLE: position_types (Reference Data)
-- =====================================================
CREATE TABLE position_types (
    code            VARCHAR(20) PRIMARY KEY, -- 'TECH_PIC'
    name            VARCHAR(100) NOT NULL,
    description     TEXT,
    sort_order      INT NOT NULL UNIQUE,
    is_active       BOOLEAN DEFAULT TRUE,
    
    -- Audit
    created_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
);

-- Seed Data (Essential for foreign keys)
INSERT INTO position_types (code, name, sort_order) VALUES
('TECH_PIC',       'Technical Lead',    1),
('TECH_BACKUP',    'Technical Backup',  2),
('FUNC_PIC',       'Functional Lead',   3),
('FUNC_BACKUP',    'Functional Backup', 4),
('SUPPORT_PIC',    'L1 Support Lead',   5),
('SUPPORT_BACKUP', 'L1 Support Backup', 6);

-- =====================================================
-- 5. TABLE: project_assignments
-- =====================================================
CREATE TABLE project_assignments (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- 🔗 Relationship
    project_id      UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    -- 🛡️ SAFETY: Prevent deleting users who are currently assigned
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    position_code   VARCHAR(20) NOT NULL REFERENCES position_types(code),
    
    -- Audit & Soft Delete
    created_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by      UUID,
    updated_at      TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by      UUID,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMP WITHOUT TIME ZONE,
    deleted_by      UUID
);

-- 🛡️ HARDENING: Logic Constraints

-- Rule 1: One position type per project (e.g. Only 1 Tech PIC per project)
CREATE UNIQUE INDEX idx_assignments_unique_pos_active 
    ON project_assignments (project_id, position_code) 
    WHERE is_deleted = FALSE;

-- Rule 2: [COMMENTED OUT] User Multi-Role Constraint
-- We removed this to allow Small Teams to function.
-- If enabled, this would prevent John from being both PIC and Backup.
/*
CREATE UNIQUE INDEX idx_assignments_unique_user_active 
    ON project_assignments (project_id, user_id) 
    WHERE is_deleted = FALSE;
*/

CREATE INDEX idx_assignments_project ON project_assignments(project_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_assignments_user ON project_assignments(user_id) WHERE is_deleted = FALSE;

-- =====================================================
-- ⚡ AUTOMATION: Auto-Update Timestamp
-- =====================================================
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
   NEW.updated_at = (now() AT TIME ZONE 'utc');
   RETURN NEW;
END;
$$ LANGUAGE 'plpgsql';

-- Apply to all tables
CREATE TRIGGER update_clients_ts BEFORE UPDATE ON clients FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_projects_ts BEFORE UPDATE ON projects FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_mappings_ts BEFORE UPDATE ON client_project_mappings FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_positions_ts BEFORE UPDATE ON position_types FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_assignments_ts BEFORE UPDATE ON project_assignments FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();