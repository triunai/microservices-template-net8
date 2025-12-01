-- =====================================================
-- 05: STANDARDIZE STATUS FIELDS (CRITICAL CONSISTENCY FIX)
-- =====================================================
-- Version: 1.0
-- Date: 2025-12-01
-- Author: Senior Engineering Review
--
-- Purpose: Fix schema inconsistency where Portal Routing domain
-- uses BOTH VARCHAR status AND BOOLEAN is_active.
--
-- Problem:
-- - clients.status        = VARCHAR (Active/Inactive)
-- - projects.status       = VARCHAR (Active/Inactive)
-- - mappings.is_active    = BOOLEAN (true/false)  ❌ INCONSISTENT
-- - position_types.is_active = BOOLEAN (true/false) ❌ INCONSISTENT
--
-- Solution: Standardize ALL Portal Routing entities to VARCHAR status
--
-- Impact:
-- - client_project_mappings: is_active → status
-- - position_types: is_active → status
--
-- Breaking Change: YES
-- - C# code must be updated (UpdateMappingCommand, DACs)
-- - Frontend code must be updated (if it uses isActive)
-- =====================================================

-- =====================================================
-- STEP 1: Migrate client_project_mappings
-- =====================================================

-- 1.1: Add the new status column (with default 'Active')
ALTER TABLE client_project_mappings
    ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'Active'
    CHECK (status IN ('Active', 'Inactive'));

-- 1.2: Migrate existing data (true → 'Active', false → 'Inactive')
UPDATE client_project_mappings
SET status = CASE
    WHEN is_active = true THEN 'Active'
    WHEN is_active = false THEN 'Inactive'
END;

-- 1.3: Drop the old boolean column
ALTER TABLE client_project_mappings
    DROP COLUMN is_active;

-- 1.4: Update indexes (Drop old, add new)
DROP INDEX IF EXISTS idx_mappings_active;
CREATE INDEX idx_mappings_status ON client_project_mappings(status) WHERE is_deleted = FALSE;

-- =====================================================
-- STEP 2: Migrate position_types
-- =====================================================

-- 2.1: Add the new status column
ALTER TABLE position_types
    ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'Active'
    CHECK (status IN ('Active', 'Inactive'));

-- 2.2: Migrate existing data
UPDATE position_types
SET status = CASE
    WHEN is_active = true THEN 'Active'
    WHEN is_active = false THEN 'Inactive'
END;

-- 2.3: Drop the old boolean column
ALTER TABLE position_types
    DROP COLUMN is_active;

-- =====================================================
-- VERIFICATION QUERIES
-- =====================================================

-- Verify client_project_mappings migration
SELECT 'client_project_mappings status distribution:' as info,
       status,
       count(*) as count
FROM client_project_mappings
WHERE is_deleted = FALSE
GROUP BY status;

-- Verify position_types migration
SELECT 'position_types status distribution:' as info,
       status,
       count(*) as count
FROM position_types
GROUP BY status;

-- =====================================================
-- ROLLBACK SCRIPT (Emergency Use Only)
-- =====================================================
-- IMPORTANT: Only use if migration fails catastrophically
-- Save your data first!
--
-- -- Rollback client_project_mappings
-- ALTER TABLE client_project_mappings ADD COLUMN is_active BOOLEAN NOT NULL DEFAULT TRUE;
-- UPDATE client_project_mappings SET is_active = (status = 'Active');
-- ALTER TABLE client_project_mappings DROP COLUMN status;
-- CREATE INDEX idx_mappings_active ON client_project_mappings(is_active) WHERE is_deleted = FALSE;
--
-- -- Rollback position_types
-- ALTER TABLE position_types ADD COLUMN is_active BOOLEAN NOT NULL DEFAULT TRUE;
-- UPDATE position_types SET is_active = (status = 'Active');
-- ALTER TABLE position_types DROP COLUMN status;
-- =====================================================
