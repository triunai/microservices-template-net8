# Frontend API Requirements Specification

## 📋 Document Purpose

This document defines all API endpoints required by the frontend application. It serves as the complete specification for the backend ASP.NET API using Fluent Results pattern.

**Backend Developer:** Please implement these endpoints exactly as specified. All responses should use the Fluent Results pattern with proper error handling.

---

## 🔐 Authentication & Authorization

### General Headers
All authenticated endpoints require:
```
Authorization: Bearer <JWT_TOKEN>
Content-Type: application/json
```

### Token Structure (JWT Claims)
```json
{
  "userId": "guid",
  "email": "string",
  "fullName": "string",
  "role": "string",
  "exp": "unix_timestamp"
}
```

### Token Lifetimes
- **Access Token:** 20 minutes
- **Refresh Token:** 60 minutes

---

## 📦 Response Format (Fluent Results)

### Success Response
```typescript
{
  "isSuccess": true,
  "value": T, // The actual data
  "errors": null
}
```

### Error Response
```typescript
{
  "isSuccess": false,
  "value": null,
  "errors": [
    {
      "code": "ERROR_CODE",
      "message": "Human-readable error message"
    }
  ]
}
```

### Common Error Codes
- `UNAUTHORIZED` - Invalid or expired token
- `FORBIDDEN` - User lacks permission
- `VALIDATION_ERROR` - Input validation failed
- `NOT_FOUND` - Resource not found
- `DUPLICATE` - Resource already exists
- `SERVER_ERROR` - Internal server error

---

## 🔑 Module 1: Authentication & Authorization

### 1.1 Login

**POST** `/api/v1/auth/login`

**Anonymous:** Yes (no token required)

**Request:**
```typescript
{
  email: string;        // Required, valid email format
  password: string;     // Required, min 8 chars
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    userId: string;              // GUID
    fullName: string;
    email: string;
    role: string;                // e.g., "Admin", "User"
    token: string;               // JWT access token
    refreshToken: string;        // JWT refresh token
    refreshTokenExpiry: string;  // ISO 8601 datetime
  },
  errors: null
}
```

**Error Responses:**
- `401` - Invalid credentials
  ```typescript
  {
    isSuccess: false,
    value: null,
    errors: [{ code: "INVALID_CREDENTIALS", message: "Invalid email or password" }]
  }
  ```
- `403` - Account locked/disabled
  ```typescript
  {
    isSuccess: false,
    value: null,
    errors: [{ code: "ACCOUNT_DISABLED", message: "Your account has been disabled" }]
  }
  ```
- `400` - Validation error
  ```typescript
  {
    isSuccess: false,
    value: null,
    errors: [{ code: "VALIDATION_ERROR", message: "Email is required" }]
  }
  ```

**Business Rules:**
- Email is case-insensitive
- Failed login attempts should be logged (security)
- Return generic error for invalid credentials (don't reveal if email exists)
- Hash passwords using bcrypt or similar

---

### 1.2 Refresh Token

**POST** `/api/v1/auth/refresh`

**Anonymous:** Yes

**Request:**
```typescript
{
  refreshToken: string;  // The refresh token from login
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    token: string;               // New JWT access token
    refreshToken: string;        // New refresh token
    refreshTokenExpiry: string;
  },
  errors: null
}
```

**Error Responses:**
- `401` - Invalid or expired refresh token

**Business Rules:**
- Invalidate old refresh token after use
- Generate new access + refresh token pair

---

### 1.3 Google OAuth Sign-In

**GET** `/api/v1/auth/google/signin`

**Anonymous:** Yes

**Request:** None (redirect endpoint)

**Response:** HTTP 302 Redirect to Google OAuth consent page

**Business Rules:**
- Redirect to Google OAuth 2.0 authorization endpoint
- Include scopes: `openid`, `email`, `profile`
- Include state parameter (CSRF protection)
- Set redirect_uri to `/api/v1/auth/google/callback`

**Google OAuth Configuration:**
- Client ID: From environment variable `GOOGLE_CLIENT_ID`
- Client Secret: From environment variable `GOOGLE_CLIENT_SECRET`
- Redirect URI: `{API_BASE_URL}/api/v1/auth/google/callback`

---

### 1.4 Google OAuth Callback

**GET** `/api/v1/auth/google/callback`

**Anonymous:** Yes

**Query Parameters:**
- `code` - Authorization code from Google
- `state` - CSRF token (must match sent state)

**Response:** HTTP 302 Redirect to frontend

**Success (200):**
Redirect to: `{FRONTEND_URL}/?token={JWT_TOKEN}&refreshToken={REFRESH_TOKEN}`

**Error:**
Redirect to: `{FRONTEND_URL}/login?error={ERROR_CODE}`

**Error Codes:**
- `oauth_failed` - OAuth flow failed
- `user_not_found` - Email not registered in system
- `account_disabled` - User account is disabled

**Business Rules:**
- Exchange authorization code for Google access token
- Fetch user info from Google (email, name, picture)
- Check if user exists in database by email
- If user doesn't exist: redirect with `error=user_not_found`
- If user exists and active: Generate JWT tokens and redirect
- If user exists but inactive: redirect with `error=account_disabled`
- Store Google profile picture URL (optional)

---

### 1.5 Microsoft OAuth Sign-In

**GET** `/api/v1/auth/microsoft/signin`

**Anonymous:** Yes

**Request:** None (redirect endpoint)

**Response:** HTTP 302 Redirect to Microsoft OAuth consent page

**Business Rules:**
- Redirect to Microsoft Identity Platform authorization endpoint
- Include scopes: `openid`, `email`, `profile`
- Include state parameter (CSRF protection)
- Set redirect_uri to `/api/v1/auth/microsoft/callback`

**Microsoft OAuth Configuration:**
- Client ID: From environment variable `MICROSOFT_CLIENT_ID`
- Client Secret: From environment variable `MICROSOFT_CLIENT_SECRET`
- Tenant ID: From environment variable `MICROSOFT_TENANT_ID` (or `common` for multi-tenant)
- Redirect URI: `{API_BASE_URL}/api/v1/auth/microsoft/callback`

---

### 1.6 Microsoft OAuth Callback

**GET** `/api/v1/auth/microsoft/callback`

**Anonymous:** Yes

**Query Parameters:**
- `code` - Authorization code from Microsoft
- `state` - CSRF token (must match sent state)

**Response:** HTTP 302 Redirect to frontend

**Success (200):**
Redirect to: `{FRONTEND_URL}/?token={JWT_TOKEN}&refreshToken={REFRESH_TOKEN}`

**Error:**
Redirect to: `{FRONTEND_URL}/login?error={ERROR_CODE}`

**Error Codes:**
- `oauth_failed` - OAuth flow failed
- `user_not_found` - Email not registered in system
- `account_disabled` - User account is disabled

**Business Rules:**
- Exchange authorization code for Microsoft access token
- Fetch user info from Microsoft Graph API (email, name, picture)
- Check if user exists in database by email
- If user doesn't exist: redirect with `error=user_not_found`
- If user exists and active: Generate JWT tokens and redirect
- If user exists but inactive: redirect with `error=account_disabled`

---

### 1.7 Forgot Password (Request Reset)

**POST** `/api/v1/auth/forgot-password`

**Anonymous:** Yes

**Request:**
```typescript
{
  email: string;  // Required
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    message: "If an account with that email exists, a password reset link has been sent."
  },
  errors: null
}
```

**Error Responses:**
- Always return success (security - don't reveal if email exists)

**Business Rules:**
- Generate secure reset token (GUID or random string)
- Store token with expiry (15 minutes)
- Send email with reset link: `https://app.example.com/change-password?token={token}`
- Return success even if email doesn't exist (security best practice)

---

### 1.4 Change Password (Complete Reset)

**POST** `/api/v1/auth/change-password`

**Anonymous:** Yes (uses reset token instead)

**Request:**
```typescript
{
  token: string;             // Reset token from email
  newPassword: string;       // Min 12 chars, uppercase, lowercase, number, special char
  confirmPassword: string;   // Must match newPassword
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    message: "Password changed successfully. You can now log in."
  },
  errors: null
}
```

**Error Responses:**
- `400` - Invalid or expired token
- `400` - Password validation failed
- `400` - Passwords don't match

**Validation Rules:**
- Password must be at least 12 characters
- Must contain at least 1 uppercase letter
- Must contain at least 1 lowercase letter
- Must contain at least 1 number
- Must contain at least 1 special character
- `newPassword` must equal `confirmPassword`

**Business Rules:**
- Token expires after 15 minutes
- Token can only be used once
- Invalidate token after successful password change
- Hash new password before storing

---

### 1.5 Get Current User Permissions

**GET** `/api/v1/auth/permissions`

**Authenticated:** Yes

**Request:** None (uses token)

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    fullName: string;
    role: string;
    modules: Array<{
      moduleName: string;      // e.g., "Portal Routing"
      subModules: Array<{
        subModuleName: string; // e.g., "Admin Navigation Console"
        canView: boolean;
        canInsert: boolean;
        canEdit: boolean;
      }>;
    }>;
  },
  errors: null
}
```

**Error Responses:**
- `401` - Invalid token
- `404` - User not found

**Business Rules:**
- Return permissions for the authenticated user (from JWT)
- Include all modules/submodules, even if all permissions are false
- Module/submodule names must be exact (used in permission checks)

**Expected Modules/SubModules:**
```
- "Dashboard" (no submodules - all users have view access)
- "Portal Routing"
  - "Client Navigation"
  - "Admin Navigation Console"
- "Task Allocation"
  - "Member Task Distribution"
  - "Summary Allocation"
- "User Maintenance"
  - "User Account"
  - "User Access Rights"
```

---

## 📊 Module 2: Dashboard

### 2.1 Get Assignment Summary

**GET** `/api/v1/dashboard/assignment-summary`

**Authenticated:** Yes

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    memberName: string;
    technicalPIC: number;      // Count of assignments
    technicalBackUp: number;
    functionalPIC: number;
    functionalBackUp: number;
    supportPIC: number;
    supportBackUp: number;
    colorCode: string;         // Hex color, e.g., "#3b82f6"
  }>,
  errors: null
}
```

**Business Rules:**
- Return assignment counts for each team member
- Include color code for each member (for chart display)
- Only include members with at least 1 assignment
- Counts are cumulative across all projects

---

### 2.2 Get Vacant Positions Summary

**GET** `/api/v1/dashboard/vacant-positions`

**Authenticated:** Yes

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    technicalPIC: number;      // Count of vacant positions
    technicalBackUp: number;
    functionalPIC: number;
    functionalBackUp: number;
    supportPIC: number;
    supportBackUp: number;
  },
  errors: null
}
```

**Business Rules:**
- Count projects where a position is not filled (null/empty)
- Return 0 if all positions are filled

---

## 🌐 Module 3: Portal Routing

### 3.1 Get All Clients

**GET** `/api/v1/portal-routing/clients`

**Authenticated:** Yes

**Permission Required:** Portal Routing → Client Navigation → canView

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    id: string;           // GUID
    clientName: string;
    logoUrl: string | null;  // Absolute URL or null
  }>,
  errors: null
}
```

**Business Rules:**
- Return all clients (no pagination for now)
- `logoUrl` can be null (frontend will use fallback)
- Order by `clientName` ascending

---

### 3.2 Get Projects by Client

**GET** `/api/v1/portal-routing/clients/{clientId}/projects`

**Authenticated:** Yes

**Permission Required:** Portal Routing → Client Navigation → canView

**Path Parameters:**
- `clientId` - GUID

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    id: string;              // GUID
    projectName: string;
    clientName: string;
    url: string;            // External URL
  }>,
  errors: null
}
```

**Error Responses:**
- `404` - Client not found

**Business Rules:**
- Return projects for the specified client
- Return empty array if client has no projects
- Order by `projectName` ascending

---

### 3.3 Get Admin Routing Mappings

**GET** `/api/v1/portal-routing/admin/mappings`

**Authenticated:** Yes

**Permission Required:** Portal Routing → Admin Navigation Console → canView

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    id: string;              // GUID (mapping ID)
    clientId: string;        // GUID
    clientName: string;
    projectId: string;       // GUID
    projectName: string;
    url: string;
    status: string;          // "Active" or "Inactive"
  }>,
  errors: null
}
```

**Business Rules:**
- Return all client-project mappings
- Order by `clientName`, then `projectName`

---

### 3.4 Get Single Admin Routing Mapping

**GET** `/api/v1/portal-routing/admin/mappings/{id}`

**Authenticated:** Yes

**Permission Required:** Portal Routing → Admin Navigation Console → canView

**Path Parameters:**
- `id` - GUID (mapping ID)

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    id: string;
    clientId: string;
    clientName: string;
    projectId: string;
    projectName: string;
    url: string;
    status: string;
  },
  errors: null
}
```

**Error Responses:**
- `404` - Mapping not found

---

### 3.5 Add Admin Routing Mapping

**POST** `/api/v1/portal-routing/admin/mappings`

**Authenticated:** Yes

**Permission Required:** Portal Routing → Admin Navigation Console → canInsert

**Request:**
```typescript
{
  clientId: string;    // GUID, required
  projectId: string;   // GUID, required
  url: string;         // Required, valid URL format
  status: string;      // "Active" or "Inactive"
}
```

**Success Response (201):**
```typescript
{
  isSuccess: true,
  value: {
    id: string;              // New mapping ID
    clientId: string;
    clientName: string;
    projectId: string;
    projectName: string;
    url: string;
    status: string;
  },
  errors: null
}
```

**Error Responses:**
- `400` - Validation error
- `404` - Client or project not found
- `409` - Mapping already exists (duplicate client-project pair)

**Validation Rules:**
- `clientId` and `projectId` must exist
- `url` must be valid URL format
- `status` must be "Active" or "Inactive"

---

### 3.6 Update Admin Routing Mapping

**PUT** `/api/v1/portal-routing/admin/mappings/{id}`

**Authenticated:** Yes

**Permission Required:** Portal Routing → Admin Navigation Console → canEdit

**Path Parameters:**
- `id` - GUID (mapping ID)

**Request:**
```typescript
{
  clientId: string;    // GUID, required
  projectId: string;   // GUID, required
  url: string;         // Required
  status: string;      // Required
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    id: string;
    clientId: string;
    clientName: string;
    projectId: string;
    projectName: string;
    url: string;
    status: string;
  },
  errors: null
}
```

**Error Responses:**
- `400` - Validation error
- `404` - Mapping not found
- `409` - Duplicate client-project pair

---

### 3.7 Get All Projects (Dropdown)

**GET** `/api/v1/portal-routing/projects`

**Authenticated:** Yes

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    id: string;         // GUID
    projectName: string;
  }>,
  errors: null
}
```

**Business Rules:**
- Return all projects (no client filtering)
- Used for dropdowns in admin forms
- Order by `projectName` ascending

---

## 👥 Module 4: Task Allocation

### 4.1 Get All Project Mappings

**GET** `/api/v1/task-allocation/project-mappings`

**Authenticated:** Yes

**Permission Required:** Task Allocation → Member Task Distribution → canView

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    mappingId: string;               // GUID
    taskPositionId: string;          // GUID
    clientId: string;                // GUID
    clientName: string;
    projectId: string;               // GUID
    projectName: string;
    technicalPICId: string | null;   // GUID or null
    technicalPICName: string | null;
    technicalBackUpId: string | null;
    technicalBackUpName: string | null;
    functionalPICId: string | null;
    functionalPICName: string | null;
    functionalBackUpId: string | null;
    functionalBackUpName: string | null;
    supportPICId: string | null;
    supportPICName: string | null;
    supportBackUpId: string | null;
    supportBackUpName: string | null;
  }>,
  errors: null
}
```

**Business Rules:**
- Return all project-member assignments
- Include user IDs and names for all 6 positions
- Positions can be null (unassigned)
- Order by `clientName`, then `projectName`

---

### 4.2 Get Team Members (Dropdown)

**GET** `/api/v1/task-allocation/team-members`

**Authenticated:** Yes

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    userId: string;      // GUID
    fullName: string;
  }>,
  errors: null
}
```

**Business Rules:**
- Return all active users
- Used for dropdowns in assignment forms
- Order by `fullName` ascending

---

### 4.3 Get Team Member Colors

**GET** `/api/v1/task-allocation/team-member-colors`

**Authenticated:** Yes

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    userId: string;      // GUID
    fullName: string;
    colorCode: string;   // Hex color, e.g., "#3b82f6"
  }>,
  errors: null
}
```

**Business Rules:**
- Return color assignments for team members
- Used for color-coding in grids and charts
- Default color if not set: "#6b7280" (gray)

---

### 4.4 Add Team Member Color

**POST** `/api/v1/task-allocation/team-member-colors`

**Authenticated:** Yes

**Permission Required:** Task Allocation → Member Task Distribution → canInsert

**Request:**
```typescript
{
  userId: string;     // GUID, required
  colorCode: string;  // Hex color, required
}
```

**Success Response (201):**
```typescript
{
  isSuccess: true,
  value: {
    userId: string;
    fullName: string;
    colorCode: string;
  },
  errors: null
}
```

**Error Responses:**
- `400` - Invalid color format
- `404` - User not found
- `409` - Color already assigned to this user

**Validation Rules:**
- `colorCode` must be valid hex color (e.g., "#3b82f6")

---

### 4.5 Add Project Assignment

**POST** `/api/v1/task-allocation/assignments`

**Authenticated:** Yes

**Permission Required:** Task Allocation → Member Task Distribution → canInsert

**Request:**
```typescript
{
  projectId: string;           // GUID, required
  clientId: string;            // GUID, required
  technicalPICId?: string;     // GUID, optional
  technicalBackUpId?: string;
  functionalPICId?: string;
  functionalBackUpId?: string;
  supportPICId?: string;
  supportBackUpId?: string;
}
```

**Success Response (201):**
```typescript
{
  isSuccess: true,
  value: {
    mappingId: string;           // New mapping ID
    taskPositionId: string;      // New task position ID
    // ... full mapping object
  },
  errors: null
}
```

**Error Responses:**
- `400` - Validation error
- `404` - Project or client not found
- `409` - Assignment already exists for this project

**Validation Rules:**
- `projectId` and `clientId` must exist
- At least one position must be assigned (not all null)
- User IDs must exist if provided

---

### 4.6 Update Task Position

**PUT** `/api/v1/task-allocation/task-positions/{taskPositionId}`

**Authenticated:** Yes

**Permission Required:** Task Allocation → Member Task Distribution → canEdit

**Path Parameters:**
- `taskPositionId` - GUID

**Request:**
```typescript
{
  positionType: "technicalPIC" | "technicalBackUp" | "functionalPIC" | "functionalBackUp" | "supportPIC" | "supportBackUp";
  userId: string | null;  // GUID or null (null = unassign)
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    taskPositionId: string;
    positionType: string;
    userId: string | null;
    userName: string | null;
  },
  errors: null
}
```

**Error Responses:**
- `400` - Invalid position type
- `404` - Task position not found
- `404` - User not found (if userId provided)

**Business Rules:**
- Setting `userId` to null unassigns the position
- Position type must be one of the 6 valid types

---

### 4.7 Delete Project Mapping

**DELETE** `/api/v1/task-allocation/project-mappings/{mappingId}`

**Authenticated:** Yes

**Permission Required:** Task Allocation → Member Task Distribution → canEdit

**Path Parameters:**
- `mappingId` - GUID

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    message: "Project mapping deleted successfully"
  },
  errors: null
}
```

**Error Responses:**
- `404` - Mapping not found

**Business Rules:**
- Delete the mapping and all associated task positions
- Cascade delete (remove all position assignments)

---

### 4.8 Add Project Type

**POST** `/api/v1/task-allocation/project-types`

**Authenticated:** Yes

**Permission Required:** Task Allocation → Member Task Distribution → canInsert

**Request:**
```typescript
{
  projectName: string;  // Required
  status: string;       // "Active" or "Inactive"
}
```

**Success Response (201):**
```typescript
{
  isSuccess: true,
  value: {
    projectId: string;   // New project ID
    projectName: string;
    status: string;
  },
  errors: null
}
```

**Error Responses:**
- `400` - Validation error
- `409` - Project name already exists

---

## 👤 Module 5: User Management

### 5.1 Get All Users

**GET** `/api/v1/user-management/users`

**Authenticated:** Yes

**Permission Required:** User Maintenance → User Account → canView

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    id: string;              // GUID
    fullName: string;
    email: string;
    contactNumber: string;
    userRoleId: string;      // GUID
    userRole: string;        // Role name
    status: boolean;         // true = active, false = inactive
    createdAt: string;       // ISO 8601 datetime
    createdBy: string;       // User who created this account
  }>,
  errors: null
}
```

**Business Rules:**
- Return all users (including inactive)
- Order by `fullName` ascending

---

### 5.2 Get Single User

**GET** `/api/v1/user-management/users/{id}`

**Authenticated:** Yes

**Permission Required:** User Maintenance → User Account → canView

**Path Parameters:**
- `id` - GUID

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    id: string;
    fullName: string;
    email: string;
    contactNumber: string;
    userRoleId: string;
    userRole: string;
    status: boolean;
    createdAt: string;
    createdBy: string;
  },
  errors: null
}
```

**Error Responses:**
- `404` - User not found

---

### 5.3 Add User

**POST** `/api/v1/user-management/users`

**Authenticated:** Yes

**Permission Required:** User Maintenance → User Account → canInsert

**Request:**
```typescript
{
  fullName: string;      // Required, 2-100 chars
  email: string;         // Required, valid email, unique
  contactNumber: string; // Required, valid phone format
  userRoleId: string;    // GUID, required
  status: boolean;       // Required
}
```

**Success Response (201):**
```typescript
{
  isSuccess: true,
  value: {
    id: string;            // New user ID
    fullName: string;
    email: string;
    contactNumber: string;
    userRoleId: string;
    userRole: string;
    status: boolean;
    createdAt: string;
    createdBy: string;     // Current user
  },
  errors: null
}
```

**Error Responses:**
- `400` - Validation error
- `404` - Role not found
- `409` - Email already exists

**Validation Rules:**
- `fullName`: 2-100 characters
- `email`: Valid email format, case-insensitive unique
- `contactNumber`: Valid phone format
- `userRoleId`: Must exist

**Business Rules:**
- Generate temporary password (send via email)
- Set `createdBy` to current user
- Set `createdAt` to current timestamp

---

### 5.4 Update User

**PUT** `/api/v1/user-management/users/{id}`

**Authenticated:** Yes

**Permission Required:** User Maintenance → User Account → canEdit

**Path Parameters:**
- `id` - GUID

**Request:**
```typescript
{
  fullName: string;
  email: string;
  contactNumber: string;
  userRoleId: string;
  status: boolean;
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    id: string;
    fullName: string;
    email: string;
    contactNumber: string;
    userRoleId: string;
    userRole: string;
    status: boolean;
    createdAt: string;
    createdBy: string;
  },
  errors: null
}
```

**Error Responses:**
- `400` - Validation error
- `404` - User not found
- `404` - Role not found
- `409` - Email already exists (for different user)

**Business Rules:**
- Cannot update own status (prevent self-lockout)
- Email uniqueness check excludes current user

---

### 5.5 Get User Roles (Dropdown)

**GET** `/api/v1/user-management/roles`

**Authenticated:** Yes

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    id: string;     // GUID
    name: string;   // e.g., "Admin", "User"
  }>,
  errors: null
}
```

**Business Rules:**
- Return all active roles
- Used for dropdowns in user forms

---

### 5.6 Get All Users for Access Rights

**GET** `/api/v1/user-management/access-rights/users`

**Authenticated:** Yes

**Permission Required:** User Maintenance → User Access Rights → canView

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    userId: string;      // GUID
    fullName: string;
    email: string;
    userRole: string;
  }>,
  errors: null
}
```

**Business Rules:**
- Return all active users
- Simplified list for access rights management

---

### 5.7 Get Permission Modules

**GET** `/api/v1/user-management/access-rights/modules`

**Authenticated:** Yes

**Permission Required:** User Maintenance → User Access Rights → canView

**Request:** None

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    id: string;          // GUID
    name: string;        // Module name
    subModules: Array<{
      id: string;        // GUID
      name: string;      // Submodule name
    }>;
  }>,
  errors: null
}
```

**Business Rules:**
- Return all modules and submodules
- Used to build permission editor UI

**Expected Data:**
```json
[
  {
    "id": "guid-1",
    "name": "Portal Routing",
    "subModules": [
      { "id": "guid-1a", "name": "Client Navigation" },
      { "id": "guid-1b", "name": "Admin Navigation Console" }
    ]
  },
  {
    "id": "guid-2",
    "name": "Task Allocation",
    "subModules": [
      { "id": "guid-2a", "name": "Member Task Distribution" },
      { "id": "guid-2b", "name": "Summary Allocation" }
    ]
  },
  {
    "id": "guid-3",
    "name": "User Maintenance",
    "subModules": [
      { "id": "guid-3a", "name": "User Account" },
      { "id": "guid-3b", "name": "User Access Rights" }
    ]
  }
]
```

---

### 5.8 Get User Permissions

**GET** `/api/v1/user-management/access-rights/users/{userId}/permissions`

**Authenticated:** Yes

**Permission Required:** User Maintenance → User Access Rights → canView

**Path Parameters:**
- `userId` - GUID

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: Array<{
    userAccessPermissionId: string;  // GUID
    moduleId: string;                // GUID
    moduleName: string;
    subModuleId: string;             // GUID
    subModuleName: string;
    canView: boolean;
    canInsert: boolean;
    canEdit: boolean;
  }>,
  errors: null
}
```

**Error Responses:**
- `404` - User not found

**Business Rules:**
- Return flat list of all permissions for user
- Include all module/submodule combinations (even if all false)
- Frontend will transform to nested structure

---

### 5.9 Update User Permissions

**PUT** `/api/v1/user-management/access-rights/users/{userId}/permissions`

**Authenticated:** Yes

**Permission Required:** User Maintenance → User Access Rights → canEdit

**Path Parameters:**
- `userId` - GUID

**Request:**
```typescript
{
  permissions: Array<{
    subModuleId: string;  // GUID
    canView: boolean;
    canInsert: boolean;
    canEdit: boolean;
  }>;
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    message: "Permissions updated successfully",
    updatedCount: number;  // Number of permissions changed
  },
  errors: null
}
```

**Error Responses:**
- `400` - Validation error
- `404` - User not found
- `404` - Submodule not found

**Validation Rules:**
- All `subModuleId` values must exist
- If `canView` is false, `canInsert` and `canEdit` must also be false
- If `canInsert` or `canEdit` is true, `canView` must also be true

**Business Rules:**
- Upsert permissions (create if not exists, update if exists)
- Delete permissions where all three flags are false
- Cannot remove own permissions (prevent self-lockout)

---

## 🔒 Security & Validation

### Global Validation Rules

1. **GUID Format:**
   - Must be valid GUID format
   - Case-insensitive

2. **Email Format:**
   - Must match standard email regex
   - Case-insensitive comparison
   - Max 255 characters

3. **String Length:**
   - All string fields have max length (default 500 unless specified)
   - Trim whitespace before validation

4. **Phone Numbers:**
   - Flexible format (allow various international formats)
   - Strip non-numeric characters before storage
   - Min 10 digits, max 15 digits

5. **URLs:**
   - Must be valid absolute URL
   - Allow http and https schemes
   - Max 2000 characters

6. **Hex Colors:**
   - Must match pattern: `^#[0-9A-Fa-f]{6}$`

### Permission Enforcement

**All endpoints must check:**
1. Valid JWT token (except anonymous endpoints)
2. Token not expired
3. User still active/exists
4. User has required permission (module → submodule → action)

**Return 403 Forbidden if:**
- User lacks required permission
- User account is inactive

**Return 401 Unauthorized if:**
- Token is missing, invalid, or expired
- Token signature is invalid

### Rate Limiting (Recommended)

- **Login endpoint:** 5 attempts per 15 minutes per IP
- **Forgot password:** 3 attempts per hour per IP
- **All other endpoints:** 100 requests per minute per user

---

## 🧪 Testing Requirements

### For Each Endpoint:

1. **Happy Path Tests:**
   - Valid request → correct response
   - Verify response structure matches spec
   - Verify business rules applied

2. **Error Cases:**
   - Invalid GUIDs
   - Missing required fields
   - Invalid field formats
   - Unauthorized access
   - Forbidden access (missing permission)
   - Not found resources

3. **Edge Cases:**
   - Empty arrays
   - Null values
   - Very long strings
   - Special characters
   - SQL injection attempts

### Sample Test Data Needed:

- 2-3 test users with different roles
- 2-3 test clients with projects
- 5-6 team members with various assignments
- Complete permission matrix for test users

---

## 📝 Notes for Backend Developer

### Critical Requirements:

1. **Use Fluent Results Pattern:**
   - ✅ Always return `{ isSuccess, value, errors }` structure
   - ✅ Use typed errors with codes and messages
   - ❌ Don't return raw exceptions to client

2. **Case-Insensitive Comparisons:**
   - Email lookups
   - Module/submodule name matching

3. **Soft Deletes (Recommended):**
   - Don't hard-delete users
   - Use `isDeleted` flag or `deletedAt` timestamp
   - Allows audit trail

4. **Audit Logging:**
   - Log all CRUD operations
   - Track: Who, What, When
   - Store user ID, action, timestamp, changed fields

5. **Data Seeding:**
   - Seed initial admin user
   - Seed all modules/submodules
   - Seed default roles (Admin, User)

6. **Connection Strings:**
   - Use environment variables
   - Separate DB for dev/staging/prod

7. **CORS Configuration:**
   - Allow frontend origin (dev: http://localhost:8080)
   - Allow credentials (cookies)
   - Whitelist required headers

### Performance Considerations:

1. **Database Indexes:**
   - Email (unique index)
   - User ID (clustered)
   - Module/submodule names
   - Foreign keys

2. **Query Optimization:**
   - Use joins instead of N+1 queries
   - Select only needed columns
   - Paginate large datasets (future)

3. **Caching (Future):**
   - Cache permission lookups
   - Cache reference data (roles, modules)

### Code Quality:

1. **Use Repository Pattern**
2. **Use Dependency Injection**
3. **Unit test business logic**
4. **Integration test endpoints**
5. **Document public methods**

---

## 🚀 Implementation Priority

### Phase 1 (Critical - Week 1):
1. Auth endpoints (login, refresh, permissions)
2. User management (get users, roles)
3. Basic error handling

### Phase 2 (Week 2):
4. Dashboard endpoints
5. Portal routing (clients, projects)

### Phase 3 (Week 3-4):
6. Portal routing admin
7. Task allocation (read operations)

### Phase 4 (Week 5-6):
8. Task allocation (write operations)
9. User access rights

### Phase 5 (Week 7-8):
10. Forgot password / change password
11. Advanced features
12. Performance optimization

---

## 📞 Questions for Backend Team?

If any specification is unclear, please ask:
- Slack: #frontend-backend-api
- Email: frontend-team@company.com

**Do not make assumptions.** Ask for clarification to ensure we're aligned.

---

**Document Version:** 1.0  
**Last Updated:** 2025-11-26  
**Frontend Team Contact:** [Your Name]
