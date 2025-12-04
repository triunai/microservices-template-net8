# User Account Management (UAM) - Frontend API Specifications

**Last Updated:** 2025-12-04  
**Backend Version:** Phase 2 Complete  
**Base URL:** `/api/v1`

---

## 📚 Table of Contents

1. [Authentication Notes](#authentication-notes)
2. [User Management](#user-management)
   - [List Users](#list-users)
   - [Get User](#get-user)
   - [Create User](#create-user)
   - [Update User](#update-user)
   - [Delete User](#delete-user)
3. [Role Management](#role-management)
   - [List Roles](#list-roles)
   - [Get Role](#get-role)
   - [Create Role](#create-role)
   - [Update Role](#update-role)
   - [Delete Role](#delete-role)
4. [User-Role Assignment](#user-role-assignment)
   - [Get User's Roles](#get-users-roles)
   - [Assign Role to User](#assign-role-to-user)
   - [Unassign Role from User](#unassign-role-from-user)
5. [User Permissions](#user-permissions)
   - [Get User Permissions](#get-user-permissions)
   - [Grant Permission](#grant-permission)
6. [TypeScript Interfaces](#typescript-interfaces)
7. [Error Handling](#error-handling)
8. [Design Notes](#design-notes)

---

## Authentication Notes

All endpoints require Bearer token authentication:
```typescript
headers: {
  'Authorization': `Bearer ${accessToken}`,
  'Content-Type': 'application/json'
}
```

---

## User Management

### List Users

**Endpoint:** `GET /api/v1/users`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `search` | string | No | Search by display name or email |

**Response (200 OK):**
```typescript
Array<{
  id: string;           // UUID
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  createdAt: string;    // ISO 8601
  createdBy: string | null;  // UUID
  updatedAt: string;    // ISO 8601
  updatedBy: string | null;  // UUID
}>
```

**Notes:**
- No pagination (internal tool, small user base ~50-100)
- Returns only non-deleted users (`is_deleted = FALSE`)
- Optional `search` query filters by `display_name ILIKE` or `email ILIKE`

---

### Get User

**Endpoint:** `GET /api/v1/users/{userId}`

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `userId` | UUID | User ID |

**Response (200 OK):**
```typescript
{
  id: string;
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  localLoginEnabled: boolean;
  ssoLoginEnabled: boolean;
  ssoProvider: string | null;    // 'azuread' | 'google' | null
  lastLoginAt: string | null;    // ISO 8601
  lastLoginProvider: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}
```

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 404 | `USER_NOT_FOUND` | User doesn't exist or is deleted |

---

### Create User

**Endpoint:** `POST /api/v1/users`

**Request Body:**
```typescript
{
  displayName: string;        // Required, max 100 chars
  email: string;              // Required, unique, valid email format
  contactNumber?: string;     // Optional
  localLoginEnabled: boolean; // Required
  password?: string;          // Required if localLoginEnabled = true
  roleIds?: string[];         // Optional, UUIDs of roles to pre-assign
}
```

**Validation Rules:**
| Field | Rule |
|-------|------|
| `displayName` | Required, max 100 characters |
| `email` | Required, valid email format, unique |
| `password` | Required if `localLoginEnabled = true` |

**Response (201 Created):**
```typescript
{
  id: string;                 // UUID of created user
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;          // Always true on creation
  localLoginEnabled: boolean;
  ssoLoginEnabled: boolean;   // Always false initially
  roles: null;                // Role assignment returns separately
  createdAt: string;
}
```

**Response Headers:**
```
Location: /api/v1/users/{userId}
```

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Validation failure (missing fields, invalid format) |
| 400 | `USER_PASSWORD_REQUIRED` | Password missing when localLoginEnabled = true |
| 409 | `USER_EMAIL_EXISTS` | Email already in use by an active user |

---

### Update User

**Endpoint:** `PUT /api/v1/users/{userId}`

**⚠️ NOT YET IMPLEMENTED** - This endpoint is planned but not yet built.

**Planned Request Body:**
```typescript
{
  displayName: string;
  contactNumber?: string;
  isActive: boolean;
}
```

**Notes:**
- Email **cannot** be changed after creation
- Password changes will be a separate endpoint

---

### Delete User

**Endpoint:** `DELETE /api/v1/users/{userId}`

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `userId` | UUID | User ID to delete |

**Response (200 OK):**
```typescript
{
  deleted: true;
  assignmentsRemoved: number;  // Count of project assignments soft-deleted
}
```

**Business Rules:**
- This is a **SOFT DELETE** (`is_deleted = TRUE`, user remains in DB)
- **Cascading:** All `project_assignments` for this user are also soft-deleted
- The `assignmentsRemoved` count can be used for a confirmation modal:
  > "This user has 5 project assignments that will be removed. Continue?"

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 404 | `USER_NOT_FOUND` | User doesn't exist or already deleted |

---

## Role Management

### List Roles

**Endpoint:** `GET /api/v1/roles`

**Response (200 OK):**
```typescript
Array<{
  id: string;           // UUID
  name: string;         // e.g., "System Administrator"
  code: string;         // e.g., "SYS_ADMIN"
  description: string | null;
  isSystem: boolean;    // true = Cannot delete, protected role
  isActive: boolean;
  userCount: number;    // Number of users with this role
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}>
```

**Notes:**
- Results are sorted: System roles first, then by name alphabetically
- `userCount` is calculated via subquery

---

### Get Role

**Endpoint:** `GET /api/v1/roles/{roleId}`

**Response (200 OK):**
```typescript
{
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  userCount: number;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}
```

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 404 | `ROLE_NOT_FOUND` | Role doesn't exist |

---

### Create Role

**Endpoint:** `POST /api/v1/roles`

**Request Body:**
```typescript
{
  name: string;           // Required, max 100 chars
  code: string;           // Required, unique, UPPER_SNAKE_CASE
  description?: string;   // Optional
  isActive?: boolean;     // Default: true
}
```

**Validation Rules:**
| Field | Rule |
|-------|------|
| `name` | Required, max 100 characters |
| `code` | Required, unique, must match `^[A-Z0-9_]+$` |

**Response (201 Created):**
```typescript
{
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: false;        // User-created roles are never system roles
  isActive: boolean;
  userCount: 0;           // New role has no users
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}
```

**Response Headers:**
```
Location: /api/v1/roles/{roleId}
```

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Validation failure |
| 409 | `ROLE_CODE_EXISTS` | Role code already exists |

---

### Update Role

**Endpoint:** `PUT /api/v1/roles/{roleId}`

**Request Body:**
```typescript
{
  name: string;           // Required
  description?: string;   // Optional
  isActive: boolean;      // Required
}
```

**⚠️ Important:** Role `code` **CANNOT** be changed after creation.

**Response (204 No Content)**

**Business Rules:**
- System roles (`isSystem = true`) can have name/description updated
- System roles **CANNOT** be deactivated (`isActive = false`)

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Validation failure |
| 403 | `ROLE_IS_SYSTEM` | Cannot deactivate a system role |
| 404 | `ROLE_NOT_FOUND` | Role doesn't exist |

---

### Delete Role

**Endpoint:** `DELETE /api/v1/roles/{roleId}`

**Response (200 OK):**
```typescript
{
  deleted: true
}
```

**Business Rules:**
- **HARD DELETE** (roles don't need audit trail)
- Cannot delete if `isSystem = true`
- Cannot delete if `userCount > 0` (users are assigned)

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 403 | `ROLE_IS_SYSTEM` | Cannot delete system role |
| 404 | `ROLE_NOT_FOUND` | Role doesn't exist |
| 409 | `ROLE_HAS_USERS` | Role has assigned users, unassign first |

---

## User-Role Assignment

### Get User's Roles

**Endpoint:** `GET /api/v1/users/{userId}/roles`

**Response (200 OK):**
```typescript
Array<{
  id: string;             // user_roles.id
  roleId: string;
  roleName: string;
  roleCode: string;
  assignedAt: string;     // ISO 8601
  assignedByName: string | null;  // Display name of who assigned
}>
```

**Notes:**
- Sorted by `assignedAt DESC` (most recent first)
- Returns empty array `[]` if user has no roles

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 404 | `USER_NOT_FOUND` | User doesn't exist |

---

### Assign Role to User

**Endpoint:** `POST /api/v1/users/{userId}/roles`

**Request Body:**
```typescript
{
  roleId: string;  // UUID of role to assign
}
```

**Response (201 Created) - New Assignment:**
```typescript
{
  id: string;        // user_roles.id
  userId: string;
  roleId: string;
  assignedAt: string;
}
```

**Response (200 OK) - Already Assigned:**
```typescript
{
  message: "Role was already assigned"
}
```

**Notes:**
- **Idempotent:** If role already assigned, returns 200 (not an error)
- Both user and role must exist

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `VALIDATION_ERROR` | Invalid UUID format |
| 404 | `USER_NOT_FOUND` | User doesn't exist |
| 404 | `ROLE_NOT_FOUND` | Role doesn't exist |

---

### Unassign Role from User

**Endpoint:** `DELETE /api/v1/users/{userId}/roles/{roleId}`

**Response (200 OK):**
```typescript
{
  deleted: true
}
```

**Notes:**
- **Idempotent:** If role not assigned, still returns 200 (not an error)
- **HARD DELETE** from `user_roles` table

**Error Responses:**
| Status | Error Code | Description |
|--------|------------|-------------|
| 404 | `USER_NOT_FOUND` | User doesn't exist |

---

## User Permissions

### Get User Permissions

**Endpoint:** `GET /api/v1/users/{userId}/permissions`

**Response (200 OK):**
```typescript
Array<{
  module: string;      // e.g., "PORTAL_ROUTING"
  subModule: string;   // e.g., "CLIENT_NAV"
  canView: boolean;
  canInsert: boolean;
  canEdit: boolean;
  canDelete: boolean;
}>
```

**Notes:**
- Returns **effective permissions** (combines role permissions + user overrides)
- Flat list, not hierarchical
- Grouped by module/subModule

---

### Grant Permission

**Endpoint:** `POST /api/v1/permissions/grant`

**Request Body:**
```typescript
{
  userId: string;       // UUID
  module: string;       // Module code, e.g., "PORTAL_ROUTING"
  subModule: string;    // SubModule code, e.g., "CLIENT_NAV"
  canView: boolean;
  canInsert: boolean;
  canEdit: boolean;
  canDelete: boolean;
}
```

**Response (200 OK):**
```typescript
{
  success: true
}
```

**Notes:**
- Creates or updates the user's permission override
- Overrides take precedence over role permissions

---

## TypeScript Interfaces

Copy these to your `model/types.ts`:

```typescript
// === USER TYPES ===
export interface User {
  id: string;
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}

export interface UserDetail extends User {
  localLoginEnabled: boolean;
  ssoLoginEnabled: boolean;
  ssoProvider: string | null;
  lastLoginAt: string | null;
  lastLoginProvider: string | null;
}

export interface CreateUserRequest {
  displayName: string;
  email: string;
  contactNumber?: string;
  localLoginEnabled: boolean;
  password?: string;
  roleIds?: string[];
}

export interface CreateUserResponse {
  id: string;
  displayName: string;
  email: string;
  contactNumber: string | null;
  isActive: boolean;
  localLoginEnabled: boolean;
  ssoLoginEnabled: boolean;
  roles: null;
  createdAt: string;
}

export interface DeleteUserResponse {
  deleted: true;
  assignmentsRemoved: number;
}

// === ROLE TYPES ===
export interface Role {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  userCount: number;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}

export interface CreateRoleRequest {
  name: string;
  code: string;
  description?: string;
  isActive?: boolean;
}

export interface UpdateRoleRequest {
  name: string;
  description?: string;
  isActive: boolean;
}

// === USER-ROLE TYPES ===
export interface UserRole {
  id: string;
  roleId: string;
  roleName: string;
  roleCode: string;
  assignedAt: string;
  assignedByName: string | null;
}

export interface AssignRoleRequest {
  roleId: string;
}

// === PERMISSION TYPES ===
export interface UserPermission {
  module: string;
  subModule: string;
  canView: boolean;
  canInsert: boolean;
  canEdit: boolean;
  canDelete: boolean;
}

export interface GrantPermissionRequest {
  userId: string;
  module: string;
  subModule: string;
  canView: boolean;
  canInsert: boolean;
  canEdit: boolean;
  canDelete: boolean;
}
```

---

## Error Handling

### ProblemDetails Response Format

All errors follow RFC 7807 ProblemDetails:

```typescript
interface ProblemDetails {
  type: string;          // e.g., "https://api.errors/USER_NOT_FOUND"
  title: string;         // Human-readable title
  status: number;        // HTTP status code
  detail: string;        // Detailed message
  instance: string;      // Request path
  errorCode: string;     // Machine-readable code for switch statements
  correlationId: string; // For support tickets
  traceId: string;       // Distributed tracing
  timestamp: string;     // ISO 8601
}
```

### Error Code → Status Code Mapping

| Error Code | HTTP Status | When It Occurs |
|------------|-------------|----------------|
| `VALIDATION_ERROR` | 400 | Invalid request body |
| `USER_PASSWORD_REQUIRED` | 400 | Password missing for local login |
| `UNAUTHORIZED` | 401 | Invalid/expired token |
| `FORBIDDEN` | 403 | No permission |
| `ROLE_IS_SYSTEM` | 403 | Cannot modify/delete system role |
| `USER_NOT_FOUND` | 404 | User doesn't exist |
| `ROLE_NOT_FOUND` | 404 | Role doesn't exist  |
| `USER_EMAIL_EXISTS` | 409 | Duplicate email |
| `ROLE_CODE_EXISTS` | 409 | Duplicate role code |
| `ROLE_HAS_USERS` | 409 | Role has assigned users |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

---

## Design Notes

### Architecture Answers

| Question | Answer |
|----------|--------|
| Is there pagination? | **No** - Internal tool with ~50-100 users |
| Can email be changed? | **No** - Email is immutable after creation |
| Is user delete soft? | **Yes** - Sets `is_deleted = TRUE` |
| Is role delete soft? | **No** - Hard delete, but blocked if users assigned |
| Role-based or direct permissions? | **Hybrid** - Role permissions + user overrides |
| Can user have multiple roles? | **Yes** - M:N via `user_roles` table |
| Does "Get All Permissions" exist? | **No** - Use `GET /users/{userId}/permissions` |

### UI Recommendations

1. **User List Page**
   - Search box filters by name/email
   - Show badge for SSO-enabled users
   - Show role count per user

2. **User Detail Page**
   - Tabs: Profile | Roles | Permissions
   - Profile tab: Edit form (name, contact, isActive)
   - Roles tab: List with add/remove
   - Permissions tab: Matrix grid (module × actions)

3. **Role List Page**
   - Show "System" badge for system roles
   - Show user count
   - Disable delete button if `isSystem` or `userCount > 0`

4. **Delete User Confirmation**
   ```
   ⚠️ Warning
   
   This user has 5 project assignments that will be removed.
   Are you sure you want to delete this user?
   
   [Cancel] [Delete]
   ```

---

**END OF SPECIFICATION**
