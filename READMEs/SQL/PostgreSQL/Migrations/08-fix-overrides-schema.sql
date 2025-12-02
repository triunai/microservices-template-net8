-- ==========================================
-- 8. FIX SCHEMA: Add Audit Columns to Overrides
-- ==========================================
-- Correcting the missing audit columns in user_permission_overrides

ALTER TABLE user_permission_overrides 
ADD COLUMN updated_at TIMESTAMP NOT NULL DEFAULT now(),
ADD COLUMN updated_by UUID NULL REFERENCES users(id);

-- Update existing rows (if any) to have matching created/updated
UPDATE user_permission_overrides 
SET updated_at = created_at, updated_by = created_by;
