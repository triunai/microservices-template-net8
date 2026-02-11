-- =====================================================
-- FUNCTION: update_updated_at_column()
-- Purpose: Auto-update updated_at column on row mutations
-- Created: Migration 03 (03-portal-routing-schema.sql)
-- Used by: BEFORE UPDATE triggers on all tables with updated_at column
--
-- Tables with this trigger:
--   Era 2 (Migration 03):
--     - clients          (update_clients_timestamp)
--     - projects         (update_projects_timestamp)
--     - client_project_mappings (update_mappings_timestamp)
--     - position_types   (update_position_types_timestamp)
--     - project_assignments (update_assignments_timestamp)
--
--   Era 1 (Migration 12):
--     - users            (trg_users_updated_at)
--     - modules          (trg_modules_updated_at)
--     - resources        (trg_resources_updated_at)
--     - actions          (trg_actions_updated_at)
--     - permissions      (trg_permissions_updated_at)
--     - roles            (trg_roles_updated_at)
--     - user_permission_overrides (trg_user_permission_overrides_updated_at)
--
--   Feature Flags (Migration 12):
--     - features         (trg_features_updated_at)
--     - client_features  (trg_client_features_updated_at)
-- =====================================================
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
   NEW.updated_at = (now() AT TIME ZONE 'utc');
   RETURN NEW;
END;
$$ LANGUAGE 'plpgsql';
