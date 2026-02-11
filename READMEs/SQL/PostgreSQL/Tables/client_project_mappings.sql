-- =====================================================
-- TABLE: client_project_mappings
-- Purpose: Portal routing configuration (Gateway bounded context)
-- Created: Migration 03 (03-portal-routing-schema.sql)
-- Business Rules:
--   - One project can have MULTIPLE routing URLs (multi-environment support)
--   - Routing URL must be globally unique among non-deleted rows
--   - Routing URL must follow regex pattern: /{client_code}/{path}
--   - Environment must be one of: Production, Staging, Development, UAT
--   - Deleting a mapping does NOT delete the project
--   - Deleting a project CASCADE deletes its mappings (routes are config, not data)
--   - No client_id column (redundant -- derive via project JOIN)
--   - Soft delete with full audit trail
-- =====================================================
CREATE TABLE client_project_mappings (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- Relationship
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,

    -- Routing Configuration
    routing_url VARCHAR(2048) NOT NULL,    -- e.g., "/acme/pos" (uniqueness enforced by partial index)
    environment VARCHAR(50) NOT NULL DEFAULT 'Production'
        CHECK (environment IN ('Production', 'Staging', 'Development', 'UAT')),

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
    deleted_by UUID NULL REFERENCES users(id),

    -- URL format validation: must start with /{segment}/{segment}
    CONSTRAINT chk_routing_url_pattern
        CHECK (routing_url ~ '^/[a-z0-9_-]+/[a-z0-9_-]+')
);

-- Indexes
-- Migration 03: Zombie-safe unique constraint on routing_url (allows URL reuse after soft delete)
CREATE UNIQUE INDEX idx_mappings_url_active
    ON client_project_mappings(routing_url)
    WHERE is_deleted = FALSE;

-- Migration 03: FK index for project_id lookups (active records only)
CREATE INDEX idx_mappings_project ON client_project_mappings(project_id) WHERE is_deleted = FALSE;

-- Migration 03: Filter by status for active records only
CREATE INDEX idx_mappings_status ON client_project_mappings(status) WHERE is_deleted = FALSE;

-- Triggers
-- TRIGGER: update_mappings_timestamp -> update_updated_at_column() (Migration 03)
CREATE TRIGGER update_mappings_timestamp
    BEFORE UPDATE ON client_project_mappings
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
