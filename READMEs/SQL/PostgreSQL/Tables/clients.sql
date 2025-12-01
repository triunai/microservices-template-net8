
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
