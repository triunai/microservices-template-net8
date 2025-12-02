# 🧪 Test Plan: RBAC & PositionType Fixes

## 1. Build Verification (PositionType Fix)
First, ensure the solution builds successfully. We changed `PositionType.IsActive` (bool) to `Status` (string).

Run this in your terminal:
```powershell
dotnet build
```
*If this fails, there may be other references to `IsActive` that need updating. Please report any errors.*

---

## 2. RBAC Logic Verification (The "Deadly Blindspot" Fix)

We need to verify that `GetPermissionsAsync` now correctly calculates:
`Effective = (RolePermissions + AllowedOverrides) - DeniedOverrides`

### Step 2.1: Setup Test Data (SQL)
Run the following SQL script in `rgt_space_portal` to create a controlled test scenario.

```sql
-- 1. Create a Test User
INSERT INTO users (id, display_name, email, is_active)
VALUES ('01938567-0000-7000-8000-000000000001', 'Test User', 'test.user@example.com', TRUE)
ON CONFLICT DO NOTHING;

-- 2. Create a "Project Manager" Role
INSERT INTO roles (id, name, code, is_active)
VALUES ('01938567-0000-7000-8000-000000000002', 'Project Manager', 'PROJECT_MANAGER', TRUE)
ON CONFLICT DO NOTHING;

-- 3. Grant "PROJECT_VIEW" and "PROJECT_EDIT" to the Role
-- (Assuming resource_id and action_id exist from seed data. If not, we rely on existing seeds)
-- Let's assume standard seeds exist. We'll map them dynamically:
INSERT INTO role_permissions (role_id, permission_id)
SELECT '01938567-0000-7000-8000-000000000002', p.id
FROM permissions p
WHERE p.code IN ('PROJECTS_VIEW', 'PROJECTS_EDIT')
ON CONFLICT DO NOTHING;

-- 4. Assign Role to User
INSERT INTO user_roles (user_id, role_id)
VALUES ('01938567-0000-7000-8000-000000000001', '01938567-0000-7000-8000-000000000002')
ON CONFLICT DO NOTHING;

-- 5. Add a DENY Override for "PROJECT_EDIT" (The "Exception")
INSERT INTO user_permission_overrides (user_id, permission_id, is_allowed)
SELECT '01938567-0000-7000-8000-000000000001', p.id, FALSE
FROM permissions p
WHERE p.code = 'PROJECTS_EDIT'
ON CONFLICT DO NOTHING;

-- 6. Add an ALLOW Override for "CLIENTS_VIEW" (The "Bonus")
INSERT INTO user_permission_overrides (user_id, permission_id, is_allowed)
SELECT '01938567-0000-7000-8000-000000000001', p.id, TRUE
FROM permissions p
WHERE p.code = 'CLIENTS_VIEW'
ON CONFLICT DO NOTHING;
```

### Step 2.2: Verify via API
Now, call the API to see the effective permissions.

**Request:**
`GET /api/v1/users/01938567-0000-7000-8000-000000000001/permissions`

**Expected Result:**
The JSON response should show:
1.  **Projects Module:**
    *   `can_view`: **true** (Inherited from Role)
    *   `can_edit`: **false** (Blocked by Deny Override 🛑)
2.  **Clients Module:**
    *   `can_view`: **true** (Granted by Allow Override ✅)

**Example JSON:**
```json
[
  {
    "module": "PROJECTS",
    "subModule": "PROJECT_DETAILS",
    "canView": true,
    "canEdit": false
  },
  {
    "module": "CLIENTS",
    "subModule": "CLIENT_DETAILS",
    "canView": true
  }
]
```

---

## 3. PositionType Verification

Since we updated the entity, we just need to ensure the application can read the table without crashing.

**Request:**
`GET /api/v1/task-allocation/projects/{projectId}` (or any endpoint using assignments)

**Verification:**
If the API returns 200 OK and data, the `PositionType` mapping is correct. If it throws a 500 error regarding `IsActive` column missing, the fix failed (but we already updated the code, so it should pass).
