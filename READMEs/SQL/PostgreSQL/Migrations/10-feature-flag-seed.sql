-- Migration 10: Seed module-level feature flags
-- These are the 4 core module flags used by frontend evaluation.
-- All system-wide (requires_client=false) — module visibility is not client-dependent.
-- created_by/updated_by resolved via admin user seeded in migration 02.

INSERT INTO features (id, code, name, description, is_active, requires_client, created_by, updated_by)
SELECT
    v.id::UUID, v.code, v.name, v.description, v.is_active, v.requires_client, u.id, u.id
FROM (VALUES
    ('019c4700-0001-7000-0000-000000000001', 'DASHBOARD',       'Dashboard',       'Controls visibility of the Dashboard module',       true, false),
    ('019c4700-0002-7000-0000-000000000002', 'TASK_ALLOCATION',  'Task Allocation', 'Controls visibility of the Task Allocation module',  true, false),
    ('019c4700-0003-7000-0000-000000000003', 'PORTAL_ROUTING',   'Portal Routing',  'Controls visibility of the Portal Routing module',   true, false),
    ('019c4700-0004-7000-0000-000000000004', 'USER_MANAGEMENT',  'User Management', 'Controls visibility of the User Management module',  true, false)
) AS v(id, code, name, description, is_active, requires_client)
CROSS JOIN users u
WHERE u.email = 'admin@rgtspace.com'
ON CONFLICT (id) DO NOTHING;
