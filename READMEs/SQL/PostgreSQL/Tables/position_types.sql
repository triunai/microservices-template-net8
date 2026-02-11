-- =====================================================
-- TABLE: position_types
-- Purpose: Immutable reference data for the 6 project staffing positions
-- Created: Migration 03 (03-portal-routing-schema.sql)
-- Business Rules:
--   - Seeded reference data (admin cannot add new positions)
--   - Exactly 6 positions: TECH_PIC, TECH_BACKUP, FUNC_PIC, FUNC_BACKUP, SUPPORT_PIC, SUPPORT_BACKUP
--   - Uses VARCHAR(20) code as PK (NOT UUID) for query readability
--   - sort_order controls UI display order (1-6), must be unique
--   - No soft delete columns (reference data is never deleted)
--   - No created_by/updated_by (system-seeded, not user-created)
-- =====================================================
CREATE TABLE position_types (
    -- Identity (VARCHAR PK, not UUID)
    code VARCHAR(20) PRIMARY KEY,          -- e.g., "TECH_PIC"
    name VARCHAR(100) NOT NULL,            -- e.g., "Technical Person-in-Charge"
    description TEXT NULL,
    sort_order INT NOT NULL UNIQUE,        -- Display order in UI (1-6)

    -- Status
    status VARCHAR(20) NOT NULL DEFAULT 'Active'
        CHECK (status IN ('Active', 'Inactive')),

    -- Audit (minimal -- reference data, rarely changes)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
);

-- No additional indexes (PK covers code lookups, sort_order has implicit UNIQUE index)

-- Triggers
-- TRIGGER: update_position_types_timestamp -> update_updated_at_column() (Migration 03)
CREATE TRIGGER update_position_types_timestamp
    BEFORE UPDATE ON position_types
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Documentation
COMMENT ON TABLE position_types IS
'Reference table for the 6 standard project positions. Seeded during initial setup.';
