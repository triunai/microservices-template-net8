-- ==========================================
-- 13. SEED FEATURES RBAC + ADMIN ACCOUNTS
-- ==========================================
-- Fixes: FEATURES module/resources/permissions were never seeded.
-- Migration 10 only created feature FLAG rows, not the RBAC entries
-- that PermissionLoadingMiddleware needs to construct
-- "FEATURES.LIST.VIEW", "FEATURES.GLOBAL.EDIT", etc.
--
-- Accounts (all SSO):
--   khumeren@gmail.com       — Google SSO
--   khumeren@rgtech.com.my   — Microsoft SSO
--   kent.tan@rgtech.com.my   — Microsoft SSO
--
-- Safe to re-run (all upserts use ON CONFLICT).

-- ==========================================
-- PART 1: FEATURES RBAC MODULE + RESOURCES
-- ==========================================
-- Creates the FEATURES module and 5 resources that map to
-- FeatureFlagConstants.Permissions:
--   FEATURES.LIST.VIEW        → ListView
--   FEATURES.GLOBAL.EDIT      → GlobalEdit
--   FEATURES.CLIENT.EDIT      → ClientEdit
--   FEATURES.OVERRIDE.INSERT  → OverrideInsert
--   FEATURES.OVERRIDE.DELETE  → OverrideDelete
--   FEATURES.EVALUATE.VIEW    → EvaluateView

DO $$
DECLARE
    v_mod_id UUID;
BEGIN
    -- Create FEATURES module (or get existing)
    INSERT INTO modules (id, name, code, sort_order)
    VALUES (uuid_generate_v7(), 'Feature Flags', 'FEATURES', 10)
    ON CONFLICT (code) WHERE is_deleted = FALSE DO NOTHING;

    SELECT id INTO v_mod_id FROM modules WHERE code = 'FEATURES' AND is_deleted = FALSE;

    -- Create 5 resources under FEATURES module
    INSERT INTO resources (id, module_id, name, code) VALUES
        (uuid_generate_v7(), v_mod_id, 'Feature List',       'LIST'),
        (uuid_generate_v7(), v_mod_id, 'Global Toggle',      'GLOBAL'),
        (uuid_generate_v7(), v_mod_id, 'Client Subscription', 'CLIENT'),
        (uuid_generate_v7(), v_mod_id, 'User Override',       'OVERRIDE'),
        (uuid_generate_v7(), v_mod_id, 'Feature Evaluation',  'EVALUATE')
    ON CONFLICT (module_id, code) WHERE is_deleted = FALSE DO NOTHING;

    RAISE NOTICE 'FEATURES module created with 5 resources';
END $$;

-- ==========================================
-- PART 2: GENERATE PERMISSIONS FOR FEATURES
-- ==========================================
-- Same pattern as migration 06: CROSS JOIN resources × actions.
-- Only generates for the FEATURES module to avoid duplicates.
INSERT INTO permissions (id, resource_id, action_id, code, description)
SELECT
    uuid_generate_v7(),
    r.id,
    a.id,
    r.code || '_' || a.code,
    'Permission to ' || a.name || ' ' || r.name
FROM resources r
JOIN modules m ON r.module_id = m.id
CROSS JOIN actions a
WHERE m.code = 'FEATURES'
  AND r.is_deleted = FALSE
ON CONFLICT (resource_id, action_id) DO NOTHING;

-- Verify: should produce 20 permissions (5 resources × 4 actions)
DO $$
DECLARE v_count INT;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM permissions p
    JOIN resources r ON p.resource_id = r.id
    JOIN modules m ON r.module_id = m.id
    WHERE m.code = 'FEATURES';
    RAISE NOTICE 'FEATURES permissions generated: %', v_count;
END $$;

-- ==========================================
-- PART 3: CREATE/UPDATE ADMIN USERS
-- ==========================================
-- All 3 accounts are SSO — no local passwords.
-- JIT sync (IdentitySyncService) links SSO identity on first login.

-- khumeren@gmail.com (Google SSO)
INSERT INTO users (id, display_name, email, is_active, local_login_enabled)
VALUES (uuid_generate_v7(), 'Khumeren', 'khumeren@gmail.com', TRUE, FALSE)
ON CONFLICT (email) WHERE is_deleted = FALSE
DO UPDATE SET is_active = TRUE, display_name = 'Khumeren';

-- khumeren@rgtech.com.my (Microsoft SSO)
INSERT INTO users (id, display_name, email, is_active, local_login_enabled)
VALUES (uuid_generate_v7(), 'Khumeren', 'khumeren@rgtech.com.my', TRUE, FALSE)
ON CONFLICT (email) WHERE is_deleted = FALSE
DO UPDATE SET is_active = TRUE, display_name = 'Khumeren';

-- kent.tan@rgtech.com.my (Microsoft SSO)
INSERT INTO users (id, display_name, email, is_active, local_login_enabled)
VALUES (uuid_generate_v7(), 'Kent Tan', 'kent.tan@rgtech.com.my', TRUE, FALSE)
ON CONFLICT (email) WHERE is_deleted = FALSE
DO UPDATE SET is_active = TRUE, display_name = 'Kent Tan';

-- ==========================================
-- PART 4: ASSIGN SYS_ADMIN ROLE
-- ==========================================
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM users u
CROSS JOIN roles r
WHERE u.email IN ('khumeren@gmail.com', 'khumeren@rgtech.com.my', 'kent.tan@rgtech.com.my')
  AND r.code = 'SYS_ADMIN'
  AND u.is_deleted = FALSE
ON CONFLICT (user_id, role_id) DO NOTHING;

-- ==========================================
-- PART 5: GRANT ALL PERMISSIONS TO SYS_ADMIN
-- ==========================================
-- IMPORTANT: This grants permissions for ALL modules, not just FEATURES.
-- Before this migration, SYS_ADMIN had the role but ZERO role_permissions rows.
-- Migrations 02 (role create) and 06 (permission generate) never linked them.
-- This fixes: PORTAL_ROUTING, TASK_ALLOCATION, USER_MGMT, and FEATURES.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'SYS_ADMIN'
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- ==========================================
-- PART 6: VERIFICATION
-- ==========================================
DO $$
DECLARE
    v_users    INT;
    v_roles    INT;
    v_perms    INT;
    v_features INT;
BEGIN
    SELECT COUNT(*) INTO v_users
    FROM users WHERE email IN ('khumeren@gmail.com', 'khumeren@rgtech.com.my', 'kent.tan@rgtech.com.my')
      AND is_active = TRUE AND is_deleted = FALSE;

    SELECT COUNT(*) INTO v_roles
    FROM user_roles ur
    JOIN users u ON ur.user_id = u.id
    JOIN roles r ON ur.role_id = r.id
    WHERE u.email IN ('khumeren@gmail.com', 'khumeren@rgtech.com.my', 'kent.tan@rgtech.com.my')
      AND r.code = 'SYS_ADMIN';

    SELECT COUNT(*) INTO v_perms
    FROM role_permissions rp
    JOIN roles r ON rp.role_id = r.id
    WHERE r.code = 'SYS_ADMIN';

    SELECT COUNT(*) INTO v_features
    FROM role_permissions rp
    JOIN roles r ON rp.role_id = r.id
    JOIN permissions p ON rp.permission_id = p.id
    JOIN resources res ON p.resource_id = res.id
    JOIN modules m ON res.module_id = m.id
    WHERE r.code = 'SYS_ADMIN' AND m.code = 'FEATURES';

    RAISE NOTICE '========================================';
    RAISE NOTICE 'Admin users active:       % / 3', v_users;
    RAISE NOTICE 'SYS_ADMIN assignments:    % / 3', v_roles;
    RAISE NOTICE 'SYS_ADMIN total perms:    %', v_perms;
    RAISE NOTICE 'SYS_ADMIN FEATURES perms: % (expect 20)', v_features;
    RAISE NOTICE '========================================';
END $$;
