# TASK-003: User Account Management (UAM) - Complete Implementation

**Status:** 📋 PLANNED  
**Priority:** HIGH  
**Created:** 2025-12-04  
**Estimated Effort:** 4-6 hours  

---

## 📋 Executive Summary

Implement the complete User Account Management (UAM) module including:
- Full User CRUD (Create, Read, Update, Delete)
- Full Role CRUD (Create, Read, Update, Delete) 
- User-Role Assignment (Assign/Unassign roles to users)
- Hybrid Authentication support (Local Password + SSO)

---

## 🏗️ Architecture Decisions

### Authentication Model: Hybrid
- Users can have **both** local password AND SSO login enabled
- `localLoginEnabled: true` → User can login with email/password
- `ssoLoginEnabled: true` → User can login via Google/Microsoft SSO
- Both can be `true` simultaneously

### User Deletion: Cascade with Warning
- When deleting a user, **cascade soft-delete** their project assignments
- Response includes assignment count so frontend can warn: "This user has X assignments that will be removed"

### Role Management: Full CRUD
- Admins can Create/Edit/Delete roles in UI
- Seeded roles: `System Administrator`, `Project Manager`, `Viewer`
- `is_system = true` roles cannot be deleted

### Password Policy: None (MVP)
- No complexity requirements
- No expiry
- Just store hash + salt

---

## 📊 Database Schema Reference

### `users` Table (Existing)
```sql
users (
  id UUID PRIMARY KEY,
  display_name TEXT NOT NULL,
  email TEXT NOT NULL UNIQUE,
  contact_number TEXT NULL,
  is_active BOOLEAN DEFAULT TRUE,
  
  -- Local Auth
  local_login_enabled BOOLEAN DEFAULT TRUE,
  password_hash BYTEA NULL,
  password_salt BYTEA NULL,
  password_expiry_at TIMESTAMP NULL,
  
  -- SSO Auth  
  sso_login_enabled BOOLEAN DEFAULT FALSE,
  sso_provider TEXT NULL,
  external_id TEXT NULL,
  
  -- Audit
  created_at, created_by, updated_at, updated_by,
  is_deleted, deleted_at, deleted_by
)
```

### `roles` Table (Existing)
```sql
roles (
  id UUID PRIMARY KEY,
  name TEXT NOT NULL,
  code TEXT NOT NULL UNIQUE,
  description TEXT NULL,
  is_system BOOLEAN DEFAULT FALSE,  -- Cannot delete if TRUE
  is_active BOOLEAN DEFAULT TRUE,
  created_at, created_by, updated_at, updated_by
)
```

### `user_roles` Table (Existing)
```sql
user_roles (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL REFERENCES users(id),
  role_id UUID NOT NULL REFERENCES roles(id),
  assigned_by_user_id UUID NULL,
  assigned_at TIMESTAMP DEFAULT now(),
  UNIQUE(user_id, role_id)
)
```

---

## 🎯 Implementation Plan

### Phase 1: User CRUD Completion

#### 1.1 Create User Endpoint
**Endpoint:** `POST /api/v1/users`

**Request:**
```typescript
{
  displayName: string;        // Required, max 100 chars
  email: string;              // Required, unique
  contactNumber?: string;     // Optional
  isActive?: boolean;         // Default: true
  localLoginEnabled: boolean; // If true, password required
  password?: string;          // Required if localLoginEnabled = true
  roleIds?: string[];         // Optional: Pre-assign roles
}
```

**Response (201 Created):**
```typescript
{
  id: string;
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  localLoginEnabled: boolean;
  ssoLoginEnabled: boolean;
  roles: Array<{ roleId: string; roleName: string; }>;
  createdAt: string;
}
```

**Business Rules:**
- Email must be unique (check `is_deleted = FALSE`)
- If `localLoginEnabled = true`, `password` is required
- Password is hashed with HMAC-SHA512 (salt + hash stored)
- `ssoLoginEnabled` defaults to `false` (set when user links SSO)
- Optional: Create `user_roles` entries for `roleIds`

**Error Codes:**
- `400 VALIDATION_ERROR` - Invalid input
- `409 EMAIL_EXISTS` - Email already in use

---

#### 1.2 Delete User Endpoint
**Endpoint:** `DELETE /api/v1/users/{userId}`

**Response (200 OK):**
```typescript
{
  deleted: true;
  assignmentsRemoved: number;  // Count of assignments that were soft-deleted
}
```

**Business Rules:**
- Soft delete: `is_deleted = TRUE, deleted_at = NOW(), deleted_by = currentUser`
- Cascade soft-delete all `project_assignments` for this user
- Return count of removed assignments (for frontend warning)
- System admin user cannot be deleted (optional protection)

**Error Codes:**
- `404 USER_NOT_FOUND` - User doesn't exist or already deleted

---

### Phase 2: Role Management

#### 2.1 List All Roles
**Endpoint:** `GET /api/v1/roles`

**Response:**
```typescript
Array<{
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  userCount: number;        // How many users have this role
  createdAt: string;
}>
```

---

#### 2.2 Get Role by ID
**Endpoint:** `GET /api/v1/roles/{roleId}`

**Response:**
```typescript
{
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  permissions: Array<{      // Permissions assigned to this role
    module: string;
    subModule: string;
    canView: boolean;
    canInsert: boolean;
    canEdit: boolean;
    canDelete: boolean;
  }>;
  users: Array<{            // Users with this role
    userId: string;
    displayName: string;
  }>;
  createdAt: string;
}
```

---

#### 2.3 Create Role
**Endpoint:** `POST /api/v1/roles`

**Request:**
```typescript
{
  name: string;           // Required, e.g., "Project Manager"
  code: string;           // Required, unique, e.g., "PROJ_MGR"
  description?: string;   // Optional
  isActive?: boolean;     // Default: true
}
```

**Response (201 Created):**
```typescript
{
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: false;
  isActive: boolean;
  createdAt: string;
}
```

**Business Rules:**
- `code` must be unique
- `isSystem` is always `false` for user-created roles
- `code` should be UPPER_SNAKE_CASE

---

#### 2.4 Update Role
**Endpoint:** `PUT /api/v1/roles/{roleId}`

**Request:**
```typescript
{
  name: string;
  description?: string;
  isActive: boolean;
}
```

**Business Rules:**
- Cannot update `code` after creation
- System roles (`isSystem = true`) can have name/description updated but NOT deactivated

---

#### 2.5 Delete Role
**Endpoint:** `DELETE /api/v1/roles/{roleId}`

**Business Rules:**
- Cannot delete if `isSystem = true` → `403 CANNOT_DELETE_SYSTEM_ROLE`
- Cannot delete if users are assigned → `409 ROLE_HAS_USERS`
- Hard delete (roles don't need audit trail)

---

### Phase 3: User-Role Assignment

#### 3.1 Get User's Roles
**Endpoint:** `GET /api/v1/users/{userId}/roles`

**Response:**
```typescript
Array<{
  id: string;           // user_roles.id
  roleId: string;
  roleName: string;
  roleCode: string;
  assignedAt: string;
  assignedBy: string | null;  // Display name of assigner
}>
```

---

#### 3.2 Assign Role to User
**Endpoint:** `POST /api/v1/users/{userId}/roles`

**Request:**
```typescript
{
  roleId: string;
}
```

**Response (201 Created):**
```typescript
{
  id: string;           // user_roles.id
  userId: string;
  roleId: string;
  roleName: string;
  assignedAt: string;
}
```

**Business Rules:**
- Unique constraint on (user_id, role_id)
- Returns existing record if already assigned (idempotent)

---

#### 3.3 Unassign Role from User
**Endpoint:** `DELETE /api/v1/users/{userId}/roles/{roleId}`

**Response (200 OK):**
```typescript
{ deleted: true }
```

**Business Rules:**
- Hard delete from `user_roles` table
- Idempotent: if not found, still return 200

---

### Phase 4: Seed Additional Roles

**Migration Script:**
```sql
INSERT INTO roles (id, name, code, description, is_system, is_active) VALUES
  (uuid_generate_v7(), 'System Administrator', 'SYS_ADMIN', 'Full access to all modules', TRUE, TRUE),
  (uuid_generate_v7(), 'Project Manager', 'PROJ_MGR', 'Can manage projects and assignments', FALSE, TRUE),
  (uuid_generate_v7(), 'Viewer', 'VIEWER', 'Read-only access', FALSE, TRUE)
ON CONFLICT (code) DO NOTHING;
```

---

## 📁 Files to Create/Modify

### New Files

```
Rgt.Space.API/Endpoints/Identity/
├── CreateUser/
│   └── Endpoint.cs
├── DeleteUser/
│   └── Endpoint.cs
├── GetUserRoles/
│   └── Endpoint.cs
├── AssignRole/
│   └── Endpoint.cs
├── UnassignRole/
│   └── Endpoint.cs

Rgt.Space.API/Endpoints/Roles/
├── GetRoles/
│   └── Endpoint.cs
├── GetRole/
│   └── Endpoint.cs
├── CreateRole/
│   └── Endpoint.cs
├── UpdateRole/
│   └── Endpoint.cs
├── DeleteRole/
│   └── Endpoint.cs

Rgt.Space.Core/
├── Abstractions/Identity/
│   └── IRoleReadDac.cs              (NEW)
│   └── IRoleWriteDac.cs             (NEW)
├── Domain/Contracts/Identity/
│   └── CreateUserRequest.cs         (NEW)
│   └── RoleResponse.cs              (NEW)
│   └── UserRoleResponse.cs          (NEW)
├── ReadModels/
│   └── RoleReadModel.cs             (NEW)
│   └── UserRoleReadModel.cs         (NEW)

Rgt.Space.Infrastructure/
├── Commands/Identity/
│   └── CreateUser.cs                (NEW)
│   └── DeleteUser.cs                (NEW)
│   └── AssignRole.cs                (NEW)
│   └── UnassignRole.cs              (NEW)
├── Commands/Roles/
│   └── CreateRole.cs                (NEW)
│   └── UpdateRole.cs                (NEW)
│   └── DeleteRole.cs                (NEW)
├── Queries/Identity/
│   └── GetUserRoles.cs              (NEW)
├── Queries/Roles/
│   └── GetAllRoles.cs               (NEW)
│   └── GetRoleById.cs               (NEW)
├── Persistence/Dac/Identity/
│   └── RoleReadDac.cs               (NEW)
│   └── RoleWriteDac.cs              (NEW)
```

### Modify Existing Files

```
Rgt.Space.Core/Abstractions/Identity/
└── IUserWriteDac.cs                 (Add DeleteAsync method)

Rgt.Space.Infrastructure/Persistence/Dac/Identity/
└── UserWriteDac.cs                  (Add DeleteAsync + password hashing)

Rgt.Space.Infrastructure/
└── Extensions.cs                    (Register new DACs)
```

---

## ✅ Implementation Checklist

### Phase 1: User CRUD
- [ ] Create `CreateUser` command + handler
- [ ] Create `CreateUser` endpoint
- [ ] Add password hashing utility (HMAC-SHA512)
- [ ] Create `DeleteUser` command + handler  
- [ ] Create `DeleteUser` endpoint
- [ ] Update `IUserWriteDac` interface
- [ ] Update `UserWriteDac` implementation
- [ ] Test Create User endpoint
- [ ] Test Delete User endpoint (with cascade)

### Phase 2: Role Management
- [ ] Create `IRoleReadDac` interface
- [ ] Create `IRoleWriteDac` interface
- [ ] Create `RoleReadDac` implementation
- [ ] Create `RoleWriteDac` implementation
- [ ] Create `RoleReadModel`
- [ ] Create `RoleResponse` DTO
- [ ] Create `GetAllRoles` query + endpoint
- [ ] Create `GetRoleById` query + endpoint
- [ ] Create `CreateRole` command + endpoint
- [ ] Create `UpdateRole` command + endpoint
- [ ] Create `DeleteRole` command + endpoint
- [ ] Register DACs in DI container

### Phase 3: User-Role Assignment
- [ ] Create `UserRoleReadModel`
- [ ] Create `GetUserRoles` query + endpoint
- [ ] Create `AssignRole` command + endpoint
- [ ] Create `UnassignRole` command + endpoint

### Phase 4: Seed Data & Testing
- [ ] Create migration for additional roles
- [ ] Run seed script
- [ ] Integration test: Full user lifecycle
- [ ] Integration test: Role assignment flow

---

## 🔐 Error Code Reference

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `VALIDATION_ERROR` | 400 | Invalid input data |
| `USER_NOT_FOUND` | 404 | User doesn't exist or is deleted |
| `ROLE_NOT_FOUND` | 404 | Role doesn't exist |
| `EMAIL_EXISTS` | 409 | Email already in use by active user |
| `ROLE_CODE_EXISTS` | 409 | Role code already exists |
| `ROLE_HAS_USERS` | 409 | Cannot delete role with assigned users |
| `CANNOT_DELETE_SYSTEM_ROLE` | 403 | System roles cannot be deleted |
| `PASSWORD_REQUIRED` | 400 | Password required when localLoginEnabled = true |

---

## 📝 Frontend Integration Notes

1. **Create User Modal:** 
   - Toggle for "Enable Local Login"
   - If enabled, show password field
   - Optional multi-select for pre-assigning roles

2. **Delete User:** 
   - Show confirmation: "This user has X project assignments that will be removed. Continue?"

3. **Role Management Page:**
   - List all roles with user count
   - System roles (SYS_ADMIN) show "System Role" badge and no delete button
   - Edit modal for name/description

4. **User Detail → Roles Tab:**
   - List assigned roles
   - "Add Role" button → dropdown of available roles
   - Remove role button per row

---

**END OF TASK PLAN**
