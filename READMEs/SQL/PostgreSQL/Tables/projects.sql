-- =====================================================
-- TABLE: projects
-- Purpose: Represents projects/applications owned by clients
-- Created: Migration 03 (03-portal-routing-schema.sql)
-- Business Rules:
--   - MUST belong to a client (client_id NOT NULL, no orphans)
--   - Code must be unique within the client scope (among non-deleted)
--   - Deleting a client is BLOCKED if it has projects (ON DELETE RESTRICT)
--   - external_url is the actual application URL (routing URLs are in client_project_mappings)
--   - Soft delete with full audit trail
-- =====================================================
CREATE TABLE projects (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    code VARCHAR(50) NOT NULL,             -- e.g., "POS" (unique per client)
    name VARCHAR(255) NOT NULL,            -- e.g., "Point of Sale System"

    -- Ownership
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,

    -- Project URLs
    external_url TEXT NULL,                -- e.g., "https://pos.acme.com" (the actual app)

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

-- Indexes
-- Migration 03: Zombie-safe unique constraint on (client_id, code) (allows code reuse after soft delete)
CREATE UNIQUE INDEX idx_projects_client_code_active
    ON projects(client_id, code)
    WHERE is_deleted = FALSE;

-- Migration 03: FK index for client_id lookups (active records only)
CREATE INDEX idx_projects_client ON projects(client_id) WHERE is_deleted = FALSE;

-- Migration 03: Filter by status for active records only
CREATE INDEX idx_projects_status ON projects(status) WHERE is_deleted = FALSE;

-- Triggers
-- TRIGGER: update_projects_timestamp -> update_updated_at_column() (Migration 03)
CREATE TRIGGER update_projects_timestamp
    BEFORE UPDATE ON projects
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
