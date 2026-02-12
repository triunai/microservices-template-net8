-- Migration 01a: Seed DevAdmin with hardcoded ID (test compatibility)
-- Must run AFTER 01 (creates users table) and BEFORE 02 (seeds admin with random UUID).
-- Migration 02 uses ON CONFLICT (email) DO NOTHING, so this INSERT "wins" and
-- preserves the hardcoded ID that DevCurrentUser and test helpers expect.

INSERT INTO users (id, display_name, email, is_active, local_login_enabled)
VALUES ('019ac92a-de20-7793-b8df-b88a87ea4e34', 'System Admin', 'admin@rgtspace.com', TRUE, TRUE)
ON CONFLICT (email) DO NOTHING;
