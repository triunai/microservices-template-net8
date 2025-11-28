-- =====================================================
-- TABLE: client_project_mappings
-- =====================================================
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

-- Documentation
COMMENT ON TABLE client_project_mappings IS 
'Portal routing configuration. Allows multiple URLs per project for different environments.';
