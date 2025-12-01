-- ==========================================
-- 4. TEST DATA (FOR DEVELOPMENT/TESTING)
-- ==========================================
-- Target Database: rgt_space_portal
-- Purpose: Seed sample data for testing the Portal Routing and Task Allocation features

-- 4.1 TEST CLIENTS
-- Note: Using WHERE NOT EXISTS instead of ON CONFLICT because our unique index
-- is partial (WHERE is_deleted = FALSE), which cannot be targeted by ON CONFLICT
INSERT INTO clients (id, name, code, status, created_at, updated_at)
SELECT uuid_generate_v7(), 'Acme Corporation', 'ACME', 'Active', now(), now()
WHERE NOT EXISTS (SELECT 1 FROM clients WHERE code = 'ACME' AND is_deleted = FALSE)
UNION ALL
SELECT uuid_generate_v7(), 'TechCorp Industries', 'TECHCORP', 'Active', now(), now()
WHERE NOT EXISTS (SELECT 1 FROM clients WHERE code = 'TECHCORP' AND is_deleted = FALSE)
UNION ALL
SELECT uuid_generate_v7(), '7-Eleven Malaysia', '7ELEVEN', 'Active', now(), now()
WHERE NOT EXISTS (SELECT 1 FROM clients WHERE code = '7ELEVEN' AND is_deleted = FALSE);

-- 4.2 TEST PROJECTS (linked to clients)
DO $$
DECLARE
    v_acme_id UUID;
    v_techcorp_id UUID;
    v_7eleven_id UUID;
    v_project_id UUID;
BEGIN
    -- Get client IDs
    SELECT id INTO v_acme_id FROM clients WHERE code = 'ACME';
    SELECT id INTO v_techcorp_id FROM clients WHERE code = 'TECHCORP';
    SELECT id INTO v_7eleven_id FROM clients WHERE code = '7ELEVEN';

    -- Insert projects for ACME
    INSERT INTO projects (id, client_id, name, code, external_url, status, created_at, updated_at)
    SELECT uuid_generate_v7(), v_acme_id, 'Acme POS System', 'POS', 'https://acme-pos.example.com', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM projects WHERE client_id = v_acme_id AND code = 'POS' AND is_deleted = FALSE)
    UNION ALL
    SELECT uuid_generate_v7(), v_acme_id, 'Acme Inventory', 'INVENTORY', 'https://acme-inventory.example.com', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM projects WHERE client_id = v_acme_id AND code = 'INVENTORY' AND is_deleted = FALSE);

    -- Insert projects for TECHCORP
    INSERT INTO projects (id, client_id, name, code, external_url, status, created_at, updated_at)
    SELECT uuid_generate_v7(), v_techcorp_id, 'TechCorp POS System', 'POS', 'https://techcorp-pos.example.com', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM projects WHERE client_id = v_techcorp_id AND code = 'POS' AND is_deleted = FALSE)
    UNION ALL
    SELECT uuid_generate_v7(), v_techcorp_id, 'TechCorp Analytics', 'ANALYTICS', 'https://techcorp-analytics.example.com', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM projects WHERE client_id = v_techcorp_id AND code = 'ANALYTICS' AND is_deleted = FALSE);

    -- Insert projects for 7ELEVEN
    INSERT INTO projects (id, client_id, name, code, external_url, status, created_at, updated_at)
    SELECT uuid_generate_v7(), v_7eleven_id, '7-Eleven Store Management', 'STORE', 'https://7eleven-store.example.com', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM projects WHERE client_id = v_7eleven_id AND code = 'STORE' AND is_deleted = FALSE)
    UNION ALL
    SELECT uuid_generate_v7(), v_7eleven_id, '7-Eleven Logistics', 'LOGISTICS', 'https://7eleven-logistics.example.com', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM projects WHERE client_id = v_7eleven_id AND code = 'LOGISTICS' AND is_deleted = FALSE);
END $$;

-- 4.3 TEST CLIENT-PROJECT MAPPINGS (Routing URLs)
DO $$
DECLARE
    v_project_id UUID;
BEGIN
    -- ACME POS - Production & Staging
    SELECT p.id INTO v_project_id FROM projects p 
    JOIN clients c ON p.client_id = c.id 
    WHERE c.code = 'ACME' AND p.code = 'POS';
    
    INSERT INTO client_project_mappings (id, project_id, routing_url, environment, status, created_at, updated_at)
    SELECT uuid_generate_v7(), v_project_id, '/acme/pos', 'Production', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM client_project_mappings WHERE routing_url = '/acme/pos' AND is_deleted = FALSE)
    UNION ALL
    SELECT uuid_generate_v7(), v_project_id, '/acme/pos-dev', 'Development', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM client_project_mappings WHERE routing_url = '/acme/pos-dev' AND is_deleted = FALSE);

    -- ACME Inventory
    SELECT p.id INTO v_project_id FROM projects p 
    JOIN clients c ON p.client_id = c.id 
    WHERE c.code = 'ACME' AND p.code = 'INVENTORY';
    
    INSERT INTO client_project_mappings (id, project_id, routing_url, environment, status, created_at, updated_at)
    SELECT uuid_generate_v7(), v_project_id, '/acme/inventory', 'Production', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM client_project_mappings WHERE routing_url = '/acme/inventory' AND is_deleted = FALSE);

    -- TECHCORP POS
    SELECT p.id INTO v_project_id FROM projects p 
    JOIN clients c ON p.client_id = c.id 
    WHERE c.code = 'TECHCORP' AND p.code = 'POS';
    
    INSERT INTO client_project_mappings (id, project_id, routing_url, environment, status, created_at, updated_at)
    SELECT uuid_generate_v7(), v_project_id, '/techcorp/pos', 'Production', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM client_project_mappings WHERE routing_url = '/techcorp/pos' AND is_deleted = FALSE);

    -- 7ELEVEN Store
    SELECT p.id INTO v_project_id FROM projects p 
    JOIN clients c ON p.client_id = c.id 
    WHERE c.code = '7ELEVEN' AND p.code = 'STORE';
    
    INSERT INTO client_project_mappings (id, project_id, routing_url, environment, status, created_at, updated_at)
    SELECT uuid_generate_v7(), v_project_id, '/7eleven/store', 'Production', 'Active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM client_project_mappings WHERE routing_url = '/7eleven/store' AND is_deleted = FALSE);
END $$;

-- 4.4 TEST USERS (for Task Allocation testing)
INSERT INTO users (id, display_name, email, contact_number, is_active, local_login_enabled, sso_login_enabled, created_at, updated_at) VALUES
    (uuid_generate_v7(), 'Ahmad bin Abdullah - Tech Lead', 'ahmad.abdullah@example.com', '+60123456789', true, false, true, now(), now()),
    (uuid_generate_v7(), 'Siti Nurhaliza - Developer', 'siti.nurhaliza@example.com', '+60123456788', true, false, true, now(), now()),
    (uuid_generate_v7(), 'Raj Kumar - Support', 'raj.kumar@example.com', '+60123456787', true, false, true, now(), now()),
    (uuid_generate_v7(), 'Mei Ling Tan - Functional Lead', 'meiling.tan@example.com', '+60123456786', true, false, true, now(), now())
ON CONFLICT (email) DO NOTHING;

-- 4.5 TEST PROJECT ASSIGNMENTS (Staffing Matrix)
DO $$
DECLARE
    v_project_id UUID;
    v_user_john UUID;
    v_user_jane UUID;
    v_user_bob UUID;
    v_user_alice UUID;
BEGIN
    -- Get user IDs
    SELECT id INTO v_user_john FROM users WHERE email = 'ahmad.abdullah@example.com';
    SELECT id INTO v_user_jane FROM users WHERE email = 'siti.nurhaliza@example.com';
    SELECT id INTO v_user_bob FROM users WHERE email = 'raj.kumar@example.com';
    SELECT id INTO v_user_alice FROM users WHERE email = 'meiling.tan@example.com';

    -- ACME POS assignments
    SELECT p.id INTO v_project_id FROM projects p 
    JOIN clients c ON p.client_id = c.id 
    WHERE c.code = 'ACME' AND p.code = 'POS';
    
    INSERT INTO project_assignments (id, project_id, user_id, position_code, created_at, updated_at)
    SELECT uuid_generate_v7(), v_project_id, v_user_john, 'TECH_PIC', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM project_assignments WHERE project_id = v_project_id AND user_id = v_user_john AND position_code = 'TECH_PIC' AND is_deleted = FALSE)
    UNION ALL
    SELECT uuid_generate_v7(), v_project_id, v_user_jane, 'TECH_BACKUP', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM project_assignments WHERE project_id = v_project_id AND user_id = v_user_jane AND position_code = 'TECH_BACKUP' AND is_deleted = FALSE)
    UNION ALL
    SELECT uuid_generate_v7(), v_project_id, v_user_alice, 'FUNC_PIC', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM project_assignments WHERE project_id = v_project_id AND user_id = v_user_alice AND position_code = 'FUNC_PIC' AND is_deleted = FALSE)
    UNION ALL
    SELECT uuid_generate_v7(), v_project_id, v_user_bob, 'SUPPORT_PIC', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM project_assignments WHERE project_id = v_project_id AND user_id = v_user_bob AND position_code = 'SUPPORT_PIC' AND is_deleted = FALSE);

    -- TECHCORP POS assignments
    SELECT p.id INTO v_project_id FROM projects p 
    JOIN clients c ON p.client_id = c.id 
    WHERE c.code = 'TECHCORP' AND p.code = 'POS';
    
    INSERT INTO project_assignments (id, project_id, user_id, position_code, created_at, updated_at)
    SELECT uuid_generate_v7(), v_project_id, v_user_jane, 'TECH_PIC', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM project_assignments WHERE project_id = v_project_id AND user_id = v_user_jane AND position_code = 'TECH_PIC' AND is_deleted = FALSE)
    UNION ALL
    SELECT uuid_generate_v7(), v_project_id, v_user_bob, 'SUPPORT_BACKUP', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM project_assignments WHERE project_id = v_project_id AND user_id = v_user_bob AND position_code = 'SUPPORT_BACKUP' AND is_deleted = FALSE);
END $$;

-- Verification queries
SELECT 'Clients created:' as info, count(*) as count FROM clients WHERE is_deleted = FALSE;
SELECT 'Projects created:' as info, count(*) as count FROM projects WHERE is_deleted = FALSE;
SELECT 'Mappings created:' as info, count(*) as count FROM client_project_mappings WHERE is_deleted = FALSE;
SELECT 'Users created:' as info, count(*) as count FROM users WHERE is_deleted = FALSE;
SELECT 'Assignments created:' as info, count(*) as count FROM project_assignments WHERE is_deleted = FALSE;
