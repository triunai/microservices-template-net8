# 🔐 User Account Management (UAM) - Complete API Specifications

**Version:** 2.0  
**Date:** 2025-12-04  
**Status:** AUTHORITATIVE FRONTEND INTEGRATION GUIDE  
**Target Audience:** Frontend Developers  

---

## 📋 Quick Reference Summary

### User Endpoints
| Operation | Method | Endpoint | Status |
|-----------|--------|----------|--------|
| List Users | GET | `/api/v1/users` | ✅ Implemented |
| Get Single User | GET | `/api/v1/users/{userId}` | ✅ Implemented |
| **Create User** | POST | `/api/v1/users` | 🔨 To Build |
| Update User | PUT | `/api/v1/users/{userId}` | ✅ Implemented |
| **Delete User** | DELETE | `/api/v1/users/{userId}` | 🔨 To Build |

### Permission Endpoints
| Operation | Method | Endpoint | Status |
|-----------|--------|----------|--------|
| Get User Permissions | GET | `/api/v1/users/{userId}/permissions` | ✅ Implemented |
| Grant Permission | POST | `/api/v1/users/{userId}/permissions/grant` | ✅ Implemented |
| Revoke Permission | POST | `/api/v1/users/{userId}/permissions/revoke` | ✅ Implemented |

### Role Endpoints
| Operation | Method | Endpoint | Status |
|-----------|--------|----------|--------|
| **List Roles** | GET | `/api/v1/roles` | 🔨 To Build |
| **Get Role** | GET | `/api/v1/roles/{roleId}` | 🔨 To Build |
| **Create Role** | POST | `/api/v1/roles` | 🔨 To Build |
| **Update Role** | PUT | `/api/v1/roles/{roleId}` | 🔨 To Build |
| **Delete Role** | DELETE | `/api/v1/roles/{roleId}` | 🔨 To Build |

### User-Role Assignment Endpoints
| Operation | Method | Endpoint | Status |
|-----------|--------|----------|--------|
| **Get User's Roles** | GET | `/api/v1/users/{userId}/roles` | 🔨 To Build |
| **Assign Role** | POST | `/api/v1/users/{userId}/roles` | 🔨 To Build |
| **Unassign Role** | DELETE | `/api/v1/users/{userId}/roles/{roleId}` | 🔨 To Build |

---

## 🏛️ Architecture Context

### Authentication Model: HYBRID
- Users can login with **local password** AND/OR **SSO (Google/Microsoft)**
- `localLoginEnabled: true` → User can login with email/password
- `ssoLoginEnabled: true` → User can login via SSO
- Both can be `true` simultaneously for the same user

### User Scope
- **Users are GLOBAL** - they exist at the system level, not per-tenant
- Users can be manually created by admin OR auto-provisioned via SSO
- When SSO user logs in, their SSO identity is linked to existing user (by email match)

### Role Scope
- **Roles are GLOBAL** - no tenant isolation
- Users can have multiple roles
- Effective permissions = Union of all role permissions + individual overrides

---

## 📚 USER ENDPOINTS

### 1. List Users

**GET** `/api/v1/users`

Returns a list of all active (non-deleted) users, optionally filtered by search term.

#### Query Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `searchTerm` | string | No | Filter by display name or email (case-insensitive) |

#### Response (200 OK)
```typescript
interface UserResponse {
  id: string;              // UUID v7
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  createdAt: string;       // ISO 8601 (UTC)
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}

type Response = UserResponse[];
```

---

### 2. Get Single User

**GET** `/api/v1/users/{userId}`

#### Response (200 OK)
```typescript
interface UserResponse {
  id: string;
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  localLoginEnabled: boolean;   // Can use password login
  ssoLoginEnabled: boolean;     // Can use SSO login
  ssoProvider: string | null;   // "AzureAD" | "Google" | null
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}
```

#### Error Responses
| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `USER_ID_REQUIRED` | UserId was empty or invalid |
| 404 | `USER_NOT_FOUND` | User does not exist or is deleted |

---

### 3. Create User 🔨

**POST** `/api/v1/users`

Creates a new user with optional local password and role assignments.

#### Request Body
```typescript
interface CreateUserRequest {
  displayName: string;        // Required, max 100 chars
  email: string;              // Required, unique
  contactNumber?: string;     // Optional
  isActive?: boolean;         // Default: true
  localLoginEnabled: boolean; // If true, password is required
  password?: string;          // Required if localLoginEnabled = true
  roleIds?: string[];         // Optional: Pre-assign role UUIDs
}
```

#### Response (201 Created)
```typescript
interface CreateUserResponse {
  id: string;
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  localLoginEnabled: boolean;
  ssoLoginEnabled: boolean;   // Always false on create
  roles: Array<{
    roleId: string;
    roleName: string;
  }>;
  createdAt: string;
}
```

#### Error Responses
| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Invalid input data |
| 400 | `PASSWORD_REQUIRED` | Password required when localLoginEnabled = true |
| 409 | `EMAIL_EXISTS` | Email already in use by active user |

#### Business Rules
- Email must be unique among non-deleted users
- If `localLoginEnabled = true`, `password` is required
- Password is hashed with HMAC-SHA512 and stored with salt
- `ssoLoginEnabled` is always `false` on creation (set when user links SSO)
- If `roleIds` provided, creates `user_roles` entries

---

### 4. Update User

**PUT** `/api/v1/users/{userId}`

Updates a user's profile information.

#### Request Body
```typescript
interface UpdateUserRequest {
  displayName: string;     // Required, max 100 chars
  email: string;           // Required, valid email
  contactNumber?: string;  // Optional
  isActive: boolean;       // Required
}
```

#### Response (200 OK)
```json
{}
```

#### Error Responses
| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | Validation Error | Invalid input |
| 404 | `User not found` | User doesn't exist or is deleted |
| 409 | `Email is already in use` | Email uniqueness constraint |

---

### 5. Delete User 🔨

**DELETE** `/api/v1/users/{userId}`

Soft deletes a user and **cascades to soft-delete all their project assignments**.

#### Response (200 OK)
```typescript
interface DeleteUserResponse {
  deleted: boolean;           // true
  assignmentsRemoved: number; // Count of soft-deleted assignments
}
```

#### Example Response
```json
{
  "deleted": true,
  "assignmentsRemoved": 5
}
```

#### Error Responses
| Status | Error Code | Description |
|--------|------------|-------------|
| 404 | `USER_NOT_FOUND` | User doesn't exist or already deleted |

#### Business Rules
- Soft delete: `is_deleted = TRUE, deleted_at = NOW()`
- Cascade soft-delete all `project_assignments` for this user
- Response includes count so frontend can show warning before delete

#### Frontend UX
```
⚠️ Warning: This user has 5 project assignments that will be removed.
Are you sure you want to delete "Ahmad Bin Abu"?

[Cancel] [Delete User]
```

---

## 📚 PERMISSION ENDPOINTS

### 6. Get User Permissions

**GET** `/api/v1/users/{userId}/permissions`

Returns the **effective permissions** for a user (Role Permissions + Overrides).

#### Response (200 OK)
```typescript
interface UserPermissionResponse {
  module: string;          // e.g., "PORTAL_ROUTING"
  subModule: string;       // e.g., "CLIENT_NAV"
  canView: boolean;
  canInsert: boolean;
  canEdit: boolean;
  canDelete: boolean;
}

type Response = UserPermissionResponse[];
```

---

### 7. Grant Permission

**POST** `/api/v1/users/{userId}/permissions/grant`

Grants specific permission overrides to a user.

#### Request Body
```typescript
interface GrantPermissionRequest {
  module: string;          // Module code, e.g., "PORTAL_ROUTING"
  subModule: string;       // SubModule code, e.g., "ADMIN_NAV"
  permissions: {
    canView: boolean;
    canInsert: boolean;
    canEdit: boolean;
    canDelete: boolean;
  };
}
```

#### Response (200 OK)
```json
{}
```

---

### 8. Revoke Permission

**POST** `/api/v1/users/{userId}/permissions/revoke`

Removes all permission overrides for a module/subModule.

#### Request Body
```typescript
interface RevokePermissionRequest {
  module: string;
  subModule: string;
}
```

#### Response (200 OK)
```json
{}
```

---

## 📚 ROLE ENDPOINTS 🔨

### 9. List All Roles

**GET** `/api/v1/roles`

#### Response (200 OK)
```typescript
interface RoleListItem {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;        // true = cannot be deleted
  isActive: boolean;
  userCount: number;        // How many users have this role
  createdAt: string;
}

type Response = RoleListItem[];
```

#### Example Response
```json
[
  {
    "id": "018d1234-5678-7abc-def0-123456789abc",
    "name": "System Administrator",
    "code": "SYS_ADMIN",
    "description": "Full access to all modules",
    "isSystem": true,
    "isActive": true,
    "userCount": 2,
    "createdAt": "2025-01-01T00:00:00Z"
  },
  {
    "id": "018d2345-5678-7abc-def0-123456789abc",
    "name": "Project Manager",
    "code": "PROJ_MGR",
    "description": "Can manage projects and assignments",
    "isSystem": false,
    "isActive": true,
    "userCount": 5,
    "createdAt": "2025-01-01T00:00:00Z"
  }
]
```

---

### 10. Get Role by ID

**GET** `/api/v1/roles/{roleId}`

#### Response (200 OK)
```typescript
interface RoleDetailResponse {
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

### 11. Create Role

**POST** `/api/v1/roles`

#### Request Body
```typescript
interface CreateRoleRequest {
  name: string;           // Required, e.g., "Project Manager"
  code: string;           // Required, unique, UPPER_SNAKE_CASE
  description?: string;   // Optional
  isActive?: boolean;     // Default: true
}
```

#### Response (201 Created)
```typescript
interface CreateRoleResponse {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;      // Always false for user-created roles
  isActive: boolean;
  createdAt: string;
}
```

#### Error Responses
| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Invalid input |
| 409 | `ROLE_CODE_EXISTS` | Role code already exists |

---

### 12. Update Role

**PUT** `/api/v1/roles/{roleId}`

#### Request Body
```typescript
interface UpdateRoleRequest {
  name: string;
  description?: string;
  isActive: boolean;
}
```

#### Response (200 OK)
```json
{}
```

#### Business Rules
- Cannot update `code` after creation
- System roles (`isSystem = true`) cannot be deactivated

---

### 13. Delete Role

**DELETE** `/api/v1/roles/{roleId}`

#### Response (200 OK)
```json
{ "deleted": true }
```

#### Error Responses
| Status | Error Code | Description |
|--------|------------|-------------|
| 403 | `CANNOT_DELETE_SYSTEM_ROLE` | System roles cannot be deleted |
| 404 | `ROLE_NOT_FOUND` | Role doesn't exist |
| 409 | `ROLE_HAS_USERS` | Cannot delete role with assigned users |

---

## 📚 USER-ROLE ASSIGNMENT ENDPOINTS 🔨

### 14. Get User's Roles

**GET** `/api/v1/users/{userId}/roles`

#### Response (200 OK)
```typescript
interface UserRoleResponse {
  id: string;           // user_roles.id (for deletion)
  roleId: string;
  roleName: string;
  roleCode: string;
  assignedAt: string;
  assignedBy: string | null;  // Display name of assigner
}

type Response = UserRoleResponse[];
```

---

### 15. Assign Role to User

**POST** `/api/v1/users/{userId}/roles`

#### Request Body
```typescript
interface AssignRoleRequest {
  roleId: string;
}
```

#### Response (201 Created)
```typescript
interface AssignRoleResponse {
  id: string;           // user_roles.id
  userId: string;
  roleId: string;
  roleName: string;
  assignedAt: string;
}
```

#### Business Rules
- Unique constraint on (user_id, role_id)
- If already assigned, returns 200 with existing record (idempotent)

---

### 16. Unassign Role from User

**DELETE** `/api/v1/users/{userId}/roles/{roleId}`

#### Response (200 OK)
```json
{ "deleted": true }
```

#### Business Rules
- Hard delete from `user_roles` table
- Idempotent: if not found, still returns 200

---

## 🎨 Frontend Integration Patterns

### TypeScript Types

```typescript
// types/user.ts

export interface User {
  id: string;
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  localLoginEnabled: boolean;
  ssoLoginEnabled: boolean;
  ssoProvider: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}

export interface CreateUserRequest {
  displayName: string;
  email: string;
  contactNumber?: string;
  isActive?: boolean;
  localLoginEnabled: boolean;
  password?: string;
  roleIds?: string[];
}

export interface UpdateUserRequest {
  displayName: string;
  email: string;
  contactNumber?: string;
  isActive: boolean;
}

export interface DeleteUserResponse {
  deleted: boolean;
  assignmentsRemoved: number;
}
```

```typescript
// types/role.ts

export interface Role {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  userCount: number;
  createdAt: string;
}

export interface RoleDetail extends Role {
  permissions: UserPermission[];
  users: Array<{ userId: string; displayName: string }>;
}

export interface CreateRoleRequest {
  name: string;
  code: string;
  description?: string;
  isActive?: boolean;
}

export interface UserRole {
  id: string;
  roleId: string;
  roleName: string;
  roleCode: string;
  assignedAt: string;
  assignedBy: string | null;
}
```

### API Client

```typescript
// api/users.ts

export const createUser = async (data: CreateUserRequest): Promise<User> => {
  const response = await apiClient.post('/api/v1/users', data);
  return response.data;
};

export const deleteUser = async (userId: string): Promise<DeleteUserResponse> => {
  const response = await apiClient.delete(`/api/v1/users/${userId}`);
  return response.data;
};

export const getUserRoles = async (userId: string): Promise<UserRole[]> => {
  const response = await apiClient.get(`/api/v1/users/${userId}/roles`);
  return response.data;
};

export const assignRole = async (userId: string, roleId: string): Promise<void> => {
  await apiClient.post(`/api/v1/users/${userId}/roles`, { roleId });
};

export const unassignRole = async (userId: string, roleId: string): Promise<void> => {
  await apiClient.delete(`/api/v1/users/${userId}/roles/${roleId}`);
};
```

```typescript
// api/roles.ts

export const getRoles = async (): Promise<Role[]> => {
  const response = await apiClient.get('/api/v1/roles');
  return response.data;
};

export const getRole = async (roleId: string): Promise<RoleDetail> => {
  const response = await apiClient.get(`/api/v1/roles/${roleId}`);
  return response.data;
};

export const createRole = async (data: CreateRoleRequest): Promise<Role> => {
  const response = await apiClient.post('/api/v1/roles', data);
  return response.data;
};

export const updateRole = async (roleId: string, data: UpdateRoleRequest): Promise<void> => {
  await apiClient.put(`/api/v1/roles/${roleId}`, data);
};

export const deleteRole = async (roleId: string): Promise<void> => {
  await apiClient.delete(`/api/v1/roles/${roleId}`);
};
```

### React Query Hooks

```typescript
// hooks/useUsers.ts

export const useCreateUser = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (data: CreateUserRequest) => usersApi.createUser(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });
};

export const useDeleteUser = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (userId: string) => usersApi.deleteUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });
};

export const useUserRoles = (userId: string) => {
  return useQuery({
    queryKey: ['users', userId, 'roles'],
    queryFn: () => usersApi.getUserRoles(userId),
    enabled: !!userId,
  });
};

export const useAssignRole = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ userId, roleId }: { userId: string; roleId: string }) =>
      usersApi.assignRole(userId, roleId),
    onSuccess: (_, { userId }) => {
      queryClient.invalidateQueries({ queryKey: ['users', userId, 'roles'] });
      queryClient.invalidateQueries({ queryKey: ['roles'] });
    },
  });
};
```

```typescript
// hooks/useRoles.ts

export const useRoles = () => {
  return useQuery({
    queryKey: ['roles'],
    queryFn: rolesApi.getRoles,
  });
};

export const useRole = (roleId: string) => {
  return useQuery({
    queryKey: ['roles', roleId],
    queryFn: () => rolesApi.getRole(roleId),
    enabled: !!roleId,
  });
};

export const useCreateRole = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (data: CreateRoleRequest) => rolesApi.createRole(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['roles'] });
    },
  });
};

export const useDeleteRole = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (roleId: string) => rolesApi.deleteRole(roleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['roles'] });
    },
  });
};
```

---

## 🏷️ Seeded Roles Reference

| Code | Name | Is System | Description |
|------|------|-----------|-------------|
| `SYS_ADMIN` | System Administrator | ✅ Yes | Full access to all modules |
| `PROJ_MGR` | Project Manager | ❌ No | Can manage projects and assignments |
| `VIEWER` | Viewer | ❌ No | Read-only access |

---

## 🔐 Error Code Reference

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `VALIDATION_ERROR` | 400 | Invalid input data |
| `PASSWORD_REQUIRED` | 400 | Password required when localLoginEnabled = true |
| `USER_NOT_FOUND` | 404 | User doesn't exist or is deleted |
| `ROLE_NOT_FOUND` | 404 | Role doesn't exist |
| `EMAIL_EXISTS` | 409 | Email already in use by active user |
| `ROLE_CODE_EXISTS` | 409 | Role code already exists |
| `ROLE_HAS_USERS` | 409 | Cannot delete role with assigned users |
| `CANNOT_DELETE_SYSTEM_ROLE` | 403 | System roles cannot be deleted |

---

## 📝 SSO Linking Flow (For Reference)

When a user who was manually created logs in via SSO for the first time:

```
1. Admin creates user in Portal:
   POST /api/v1/users { email: "ahmad@company.com", localLoginEnabled: true, password: "xxx" }
   → User created with ssoLoginEnabled = false

2. User logs in via Microsoft SSO:
   → SSO Broker authenticates
   → Portal API receives callback with SSO identity

3. Portal API links SSO identity:
   → Finds user by email match
   → Updates: ssoLoginEnabled = true, ssoProvider = "AzureAD", externalId = "xyz"

4. User can now login with BOTH password AND Microsoft SSO ✅
```

---

**END OF UAM API SPECIFICATIONS v2.0**
