
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

    -- Status (STANDARDIZED with clients/projects/mappings)
    status VARCHAR(20) NOT NULL DEFAULT 'Active'
        CHECK (status IN ('Active', 'Inactive')),

    -- Audit (reference data - rarely changes)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
);

-- Seed position types (this will be in seed script)
COMMENT ON TABLE position_types IS 
'Reference table for the 6 standard project positions. Seeded during initial setup.';
