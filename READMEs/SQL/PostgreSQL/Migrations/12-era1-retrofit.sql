-- Migration: 12-era1-retrofit.sql
-- Purpose: Retrofit Era 1 tables (migration 01) to Era 2 standards (migration 03/09)
-- Depends on: 01-portal-schema.sql, 03-portal-routing-schema.sql, 08-fix-overrides-schema.sql, 09-feature-flags.sql
-- Date: 2026-02-11
-- Source: 5-agent schema audit findings H2, H4, H5 (SCHEMA-AUDIT-MASTER.md)
--
-- Changes:
--   1. Zombie-safe partial indexes (replace plain UNIQUE on soft-deletable tables)
--   2. Missing updated_at triggers (9 tables)
--   3. Timestamp default standardization (now() → NOW() AT TIME ZONE 'utc')

-- ============================================
-- PART 1: Zombie-Safe Partial Indexes (H4, H5)
-- ============================================
-- Era 1 tables use plain UNIQUE constraints which block code reuse after soft delete.
-- Era 2 tables (migration 03) use partial indexes: WHERE is_deleted = FALSE.
-- Only tables WITH is_deleted column need conversion.
-- Tables WITHOUT is_deleted (actions, permissions, roles) keep plain UNIQUE as-is.

-- 1a. users.email — plain UNIQUE → partial index
ALTER TABLE users DROP CONSTRAINT users_email_uk;
DROP INDEX IF EXISTS idx_users_email;
CREATE UNIQUE INDEX idx_users_email_active ON users(email) WHERE is_deleted = FALSE;

-- 1b. users.sso_provider + external_id — plain UNIQUE → partial index
ALTER TABLE users DROP CONSTRAINT users_sso_uk;
DROP INDEX IF EXISTS idx_users_external_id;
CREATE UNIQUE INDEX idx_users_sso_active ON users(sso_provider, external_id)
    WHERE is_deleted = FALSE AND sso_provider IS NOT NULL;

-- 1c. modules.code — plain UNIQUE → partial index
ALTER TABLE modules DROP CONSTRAINT modules_code_uk;
CREATE UNIQUE INDEX idx_modules_code_active ON modules(code) WHERE is_deleted = FALSE;

-- 1d. resources.(module_id, code) — plain UNIQUE → partial index
ALTER TABLE resources DROP CONSTRAINT resources_module_code_uk;
CREATE UNIQUE INDEX idx_resources_module_code_active ON resources(module_id, code) WHERE is_deleted = FALSE;


-- ============================================
-- PART 2: Missing updated_at Triggers (H2)
-- ============================================
-- The update_updated_at_column() function exists (created in migration 03).
-- Era 1 tables and Era 2 feature flag tables are missing auto-update triggers.
-- Using CREATE OR REPLACE TRIGGER (PostgreSQL 14+) for idempotency.

-- Era 1 tables (migration 01)
CREATE OR REPLACE TRIGGER trg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trg_modules_updated_at
    BEFORE UPDATE ON modules
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trg_resources_updated_at
    BEFORE UPDATE ON resources
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trg_actions_updated_at
    BEFORE UPDATE ON actions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trg_permissions_updated_at
    BEFORE UPDATE ON permissions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trg_roles_updated_at
    BEFORE UPDATE ON roles
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Migration 08 added updated_at to this table but no trigger
CREATE OR REPLACE TRIGGER trg_user_permission_overrides_updated_at
    BEFORE UPDATE ON user_permission_overrides
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Era 2 feature flag tables (migration 09) — missing triggers
CREATE OR REPLACE TRIGGER trg_features_updated_at
    BEFORE UPDATE ON features
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trg_client_features_updated_at
    BEFORE UPDATE ON client_features
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();


-- ============================================
-- PART 3: Timestamp Default Standardization (H2)
-- ============================================
-- Era 1 uses DEFAULT now() (server-local timezone).
-- Era 2 uses DEFAULT (now() AT TIME ZONE 'utc') (explicit UTC).
-- Standardize Era 1 to match. Only affects NEW rows.

-- users
ALTER TABLE users ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE users ALTER COLUMN updated_at SET DEFAULT (now() AT TIME ZONE 'utc');

-- modules
ALTER TABLE modules ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE modules ALTER COLUMN updated_at SET DEFAULT (now() AT TIME ZONE 'utc');

-- resources
ALTER TABLE resources ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE resources ALTER COLUMN updated_at SET DEFAULT (now() AT TIME ZONE 'utc');

-- actions
ALTER TABLE actions ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE actions ALTER COLUMN updated_at SET DEFAULT (now() AT TIME ZONE 'utc');

-- permissions
ALTER TABLE permissions ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE permissions ALTER COLUMN updated_at SET DEFAULT (now() AT TIME ZONE 'utc');

-- roles
ALTER TABLE roles ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE roles ALTER COLUMN updated_at SET DEFAULT (now() AT TIME ZONE 'utc');

-- user_roles
ALTER TABLE user_roles ALTER COLUMN assigned_at SET DEFAULT (now() AT TIME ZONE 'utc');

-- user_permission_overrides (created_at from 01, updated_at from 08)
ALTER TABLE user_permission_overrides ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
ALTER TABLE user_permission_overrides ALTER COLUMN updated_at SET DEFAULT (now() AT TIME ZONE 'utc');

-- user_sessions
ALTER TABLE user_sessions ALTER COLUMN created_at SET DEFAULT (now() AT TIME ZONE 'utc');
