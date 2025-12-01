# PostgreSQL Database Setup Guide

## 📋 Prerequisites
- PostgreSQL 18 installed
- Database: `rgt_space_portal` created
- User with sufficient privileges

## 🚀 Execution Order (CRITICAL - Must follow this sequence)

Run the SQL scripts in this exact order:

```bash
# 1. Extensions (UUID v7 function)
psql -U postgres -d rgt_space_portal -f 00-extensions.sql

# 2. Identity & RBAC Schema (Users, Sessions, Roles, Permissions)
psql -U postgres -d rgt_space_portal -f 01-portal-schema.sql

# 3. Portal Routing & Task Allocation Schema (Clients, Projects, Mappings, Assignments)
psql -U postgres -d rgt_space_portal -f 03-portal-routing-schema.sql

# 4. Reference Data (Actions, Position Types, Modules, Resources, Permissions, Roles)
psql -U postgres -d rgt_space_portal -f 02-portal-seed.sql

# 5. Test Data (Sample Clients, Projects, Users for development)
psql -U postgres -d rgt_space_portal -f 04-test-data.sql
```

## 🔍 Post-Execution Verification

After running all scripts, verify the data:

```sql
-- Check UUID v7 function exists
SELECT uuid_generate_v7();

-- Check reference data
SELECT count(*) FROM actions;          -- Should be 3 (VIEW, INSERT, EDIT)
SELECT count(*) FROM position_types;   -- Should be 6 (TECH_PIC, etc.)
SELECT count(*) FROM modules;          -- Should be 3 (Portal Routing, Task Allocation, User Maintenance)

-- Check test data
SELECT count(*) FROM clients WHERE is_deleted = FALSE;   -- Should be 3 (ACME, TECHCORP, 7ELEVEN)
SELECT count(*) FROM projects WHERE is_deleted = FALSE;  -- Should be 6
SELECT count(*) FROM client_project_mappings WHERE is_deleted = FALSE;  -- Should be 5
SELECT count(*) FROM users WHERE is_deleted = FALSE;     -- Should be 4
SELECT count(*) FROM project_assignments WHERE is_deleted = FALSE;  -- Should be 6

-- Test the pivot query (Project Assignment Matrix)
SELECT 
    p.name AS project_name,
    c.name AS client_name,
    MAX(CASE WHEN pa.position_code = 'TECH_PIC' THEN u.display_name END) AS tech_pic,
    MAX(CASE WHEN pa.position_code = 'TECH_BACKUP' THEN u.display_name END) AS tech_backup,
    MAX(CASE WHEN pa.position_code = 'FUNC_PIC' THEN u.display_name END) AS func_pic,
    MAX(CASE WHEN pa.position_code = 'SUPPORT_PIC' THEN u.display_name END) AS support_pic
FROM projects p
JOIN clients c ON p.client_id = c.id
LEFT JOIN project_assignments pa ON p.id = pa.project_id AND pa.is_deleted = FALSE
LEFT JOIN users u ON pa.user_id = u.id
WHERE p.is_deleted = FALSE
GROUP BY p.id, p.name, c.name
ORDER BY c.name, p.name;
```

Expected output for the pivot query:
```
project_name             | client_name         | tech_pic          | tech_backup        | func_pic           | support_pic
-------------------------+--------------------+-------------------+-------------------+-------------------+------------------
Acme Inventory           | Acme Corporation   | [NULL]            | [NULL]            | [NULL]            | [NULL]
Acme POS System          | Acme Corporation   | John Doe - Tech Lead | Jane Smith - Developer | Alice Brown - Functional Lead | Bob Wilson - Support
TechCorp Analytics       | TechCorp Industries| [NULL]            | [NULL]            | [NULL]            | [NULL]
TechCorp POS System      | TechCorp Industries| Jane Smith - Developer | [NULL]            | [NULL]            | [NULL]
7-Eleven Logistics       | 7-Eleven Malaysia  | [NULL]            | [NULL]            | [NULL]            | [NULL]
7-Eleven Store Management| 7-Eleven Malaysia  | [NULL]            | [NULL]            | [NULL]            | [NULL]
```

## 🧪 Testing the DACs

Now that you have seed data, you can test the DACs:

```csharp
// Test ClientReadDac
var clients = await _clientReadDac.GetAllAsync(ct);
// Should return: ACME, TECHCORP, 7ELEVEN

var acmeClient = await _clientReadDac.GetByCodeAsync("ACME", ct);
// Should return: Acme Corporation

// Test ProjectReadDac
var acmeProjects = await _projectReadDac.GetByClientIdAsync(acmeClient.Id, ct);
// Should return: Acme POS System, Acme Inventory

// Test ProjectAssignmentReadDac (Pivot Query)
var assignments = await _projectAssignmentReadDac.GetAllAsync(ct);
// Should return: 6 projects with pivoted position assignments
```

## 📊 Sample Data Summary

### Clients (3)
- **ACME** - Acme Corporation
- **TECHCORP** - TechCorp Industries  
- **7ELEVEN** - 7-Eleven Malaysia

### Projects (6)
- ACME:
  - `POS` - Acme POS System
  - `INVENTORY` - Acme Inventory
- TECHCORP:
  - `POS` - TechCorp POS System
  - `ANALYTICS` - TechCorp Analytics
- 7ELEVEN:
  - `STORE` - 7-Eleven Store Management
  - `LOGISTICS` - 7-Eleven Logistics

### Routing Mappings (5)
- `/acme/pos` → Acme POS (Production)
- `/acme/pos-dev` → Acme POS (Development)
- `/acme/inventory` → Acme Inventory (Production)
- `/techcorp/pos` → TechCorp POS (Production)
- `/7eleven/store` → 7-Eleven Store (Production)

### Test Users (4)
- **John Doe** - Tech Lead (john.doe@example.com)
- **Jane Smith** - Developer (jane.smith@example.com)
- **Bob Wilson** - Support (bob.wilson@example.com)
- **Alice Brown** - Functional Lead (alice.brown@example.com)

### Project Assignments (6)
- **Acme POS**:
  - Tech PIC: John Doe
  - Tech Backup: Jane Smith
  - Functional PIC: Alice Brown
  - Support PIC: Bob Wilson
- **TechCorp POS**:
  - Tech PIC: Jane Smith
  - Support Backup: Bob Wilson

## ⚠️ Troubleshooting

### Issue: "function uuid_generate_v7() does not exist"
**Solution:** Run `00-extensions.sql` first.

### Issue: Foreign key violations
**Solution:** Ensure you run scripts in the correct order (see above).

### Issue: Unique constraint violations
**Solution:** The seed scripts use `ON CONFLICT DO NOTHING`. If you're re-running, you might already have the data.

To reset and start fresh:
```sql
-- WARNING: This deletes ALL data
TRUNCATE TABLE project_assignments, client_project_mappings, projects, clients CASCADE;
TRUNCATE TABLE user_permission_overrides, user_roles, role_permissions, permissions, actions, resources, modules CASCADE;
TRUNCATE TABLE position_types CASCADE;
```

## 🎯 Next Steps

1. ✅ Run all SQL scripts in order
2. ✅ Verify data loaded correctly
3. ✅ Build and run the API
4. ✅ Test DAC methods via API endpoints
5. ✅ Check resilience (try stopping PostgreSQL mid-request to test circuit breaker)

---

**Last Updated:** 2025-11-28  
**Schema Version:** 1.1 (Platinum Hardened)
