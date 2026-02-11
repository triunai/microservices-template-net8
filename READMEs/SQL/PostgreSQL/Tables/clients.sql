-- =====================================================
-- TABLE: clients
-- Purpose: Represents client organizations in the multi-tenant POS/ERP platform
-- Created: Migration 03 (03-portal-routing-schema.sql)
-- Business Rules:
--   - Code must be globally unique among non-deleted rows (for URL prefixing)
--   - Name can duplicate (different divisions of same company)
--   - Status controls visibility in UI ('Active' or 'Inactive')
--   - Cannot delete a client that has projects (ON DELETE RESTRICT on projects.client_id)
--   - Soft delete with full audit trail
-- =====================================================
CREATE TABLE clients (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    code VARCHAR(50) NOT NULL,             -- e.g., "ACME" (globally unique, enforced by partial index)
    name VARCHAR(255) NOT NULL,            -- e.g., "Acme Corporation"

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
-- Migration 03: Zombie-safe unique constraint on code (allows reuse after soft delete)
CREATE UNIQUE INDEX idx_clients_code_active ON clients(code) WHERE is_deleted = FALSE;

-- Migration 03: Filter by status for active records only
CREATE INDEX idx_clients_status ON clients(status) WHERE is_deleted = FALSE;

-- Triggers
-- TRIGGER: update_clients_timestamp -> update_updated_at_column() (Migration 03)
CREATE TRIGGER update_clients_timestamp
    BEFORE UPDATE ON clients
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
