-- ==========================================
-- 7. SEED PERMISSION OVERRIDES (Test Data)
-- ==========================================
-- This script seeds some initial permission overrides for testing.

DO $$
DECLARE
    v_admin_id UUID;
    v_perm_view_id UUID;
    v_perm_edit_id UUID;
BEGIN
    -- 1. Get the System Admin User ID
    SELECT id INTO v_admin_id FROM users WHERE email = 'admin@rgtspace.com';
    
    IF v_admin_id IS NULL THEN
        RAISE NOTICE 'Admin user not found, skipping override seed.';
        RETURN;
    END IF;

    -- 2. Get Permission IDs for 'CLIENT_NAV'
    SELECT id INTO v_perm_view_id FROM permissions WHERE code = 'CLIENT_NAV_VIEW';
    SELECT id INTO v_perm_edit_id FROM permissions WHERE code = 'CLIENT_NAV_EDIT';

    -- 3. Grant VIEW (Allow)
    INSERT INTO user_permission_overrides (
        user_id, permission_id, is_allowed, reason, created_by, updated_by
    ) VALUES (
        v_admin_id, v_perm_view_id, TRUE, 'Seeded View Access', v_admin_id, v_admin_id
    )
    ON CONFLICT (user_id, permission_id) DO NOTHING;

    -- 4. Deny EDIT (Explicit Deny test)
    INSERT INTO user_permission_overrides (
        user_id, permission_id, is_allowed, reason, created_by, updated_by
    ) VALUES (
        v_admin_id, v_perm_edit_id, FALSE, 'Seeded Deny Access', v_admin_id, v_admin_id
    )
    ON CONFLICT (user_id, permission_id) DO NOTHING;

    RAISE NOTICE 'Seeded overrides for Admin user.';
END $$;
