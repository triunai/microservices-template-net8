-- ==========================================
-- 2. PORTAL SEED DATA
-- ==========================================
-- Target Database: rgt_space_portal_db

-- 2.1 ACTIONS
INSERT INTO actions (id, name, code) VALUES
    (uuid_generate_v7(), 'View', 'VIEW'),
    (uuid_generate_v7(), 'Insert', 'INSERT'),
    (uuid_generate_v7(), 'Edit', 'EDIT'),
    (uuid_generate_v7(), 'Delete', 'DELETE')
ON CONFLICT (code) DO NOTHING;

-- 2.2 POSITION TYPES
-- 2.2 POSITION TYPES
INSERT INTO position_types (name, code, sort_order) VALUES
    ('Technical PIC', 'TECH_PIC', 1),
    ('Technical Back-Up', 'TECH_BACKUP', 2),
    ('Functional PIC', 'FUNC_PIC', 3),
    ('Functional Back-Up', 'FUNC_BACKUP', 4),
    ('Support PIC', 'SUPPORT_PIC', 5),
    ('Support Back-Up', 'SUPPORT_BACKUP', 6)
ON CONFLICT (code) DO NOTHING;

-- 2.3 MODULES & RESOURCES (Based on Frontend)
-- Module: Portal Routing
DO $$
DECLARE
    v_mod_id UUID;
BEGIN
    INSERT INTO modules (id, name, code, sort_order) 
    VALUES (uuid_generate_v7(), 'Portal Routing', 'PORTAL_ROUTING', 1)
    RETURNING id INTO v_mod_id;
    
    INSERT INTO resources (id, module_id, name, code) VALUES
        (uuid_generate_v7(), v_mod_id, 'Client Navigation', 'CLIENT_NAV'),
        (uuid_generate_v7(), v_mod_id, 'Admin Routing', 'ADMIN_ROUTING');
END $$;

-- Module: Task Allocation
DO $$
DECLARE
    v_mod_id UUID;
BEGIN
    INSERT INTO modules (id, name, code, sort_order) 
    VALUES (uuid_generate_v7(), 'Task Allocation', 'TASK_ALLOCATION', 2)
    RETURNING id INTO v_mod_id;
    
    INSERT INTO resources (id, module_id, name, code) VALUES
        (uuid_generate_v7(), v_mod_id, 'Members Distribution', 'MEMBERS_DIST');
END $$;

-- Module: User Management
DO $$
DECLARE
    v_mod_id UUID;
BEGIN
    INSERT INTO modules (id, name, code, sort_order) 
    VALUES (uuid_generate_v7(), 'User Management', 'USER_MGMT', 3)
    RETURNING id INTO v_mod_id;
    
    INSERT INTO resources (id, module_id, name, code) VALUES
        (uuid_generate_v7(), v_mod_id, 'User Account', 'USER_ACCOUNT'),
        (uuid_generate_v7(), v_mod_id, 'User Access Rights', 'USER_ACCESS');
END $$;

-- 2.4 SYSTEM ADMIN ROLE
INSERT INTO roles (id, name, code, description, is_system, is_active) VALUES
    (uuid_generate_v7(), 'System Administrator', 'SYS_ADMIN', 'Full access to all modules', TRUE, TRUE)
ON CONFLICT (code) DO NOTHING;

-- 2.5 INITIAL ADMIN USER
-- Password: 'Password123!' (Hash this in production)
INSERT INTO users (id, display_name, email, is_active, local_login_enabled) VALUES
    (uuid_generate_v7(), 'System Admin', 'admin@rgtspace.com', TRUE, TRUE)
ON CONFLICT (email) DO NOTHING;

-- Assign Admin Role
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM users u, roles r
WHERE u.email = 'admin@rgtspace.com' AND r.code = 'SYS_ADMIN'
ON CONFLICT (user_id, role_id) DO NOTHING;
