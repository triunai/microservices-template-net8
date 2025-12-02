-- ==========================================
-- 6. PERMISSION GENERATION (The Missing Link)
-- ==========================================
-- This script ensures the 'permissions' table is populated.
-- It creates a Cartesian Product of All Resources x All Actions.

-- 1. Ensure DELETE action exists (Safe Upsert)
INSERT INTO actions (id, name, code) VALUES
    (uuid_generate_v7(), 'Delete', 'DELETE')
ON CONFLICT (code) DO NOTHING;

-- 2. Generate Permissions
-- Logic: For every Resource, create Permissions for VIEW, INSERT, EDIT, DELETE
-- Format: RESOURCE_CODE + '_' + ACTION_CODE (e.g., CLIENT_NAV_DELETE)
INSERT INTO permissions (id, resource_id, action_id, code, description)
SELECT 
    uuid_generate_v7(),
    r.id,
    a.id,
    r.code || '_' || a.code,
    'Permission to ' || a.name || ' ' || r.name
FROM resources r
CROSS JOIN actions a
ON CONFLICT (resource_id, action_id) DO NOTHING;

-- 3. Validation
DO $$
DECLARE
    v_count INT;
BEGIN
    SELECT COUNT(*) INTO v_count FROM permissions;
    RAISE NOTICE 'Total Permissions Generated: %', v_count;
END $$;
