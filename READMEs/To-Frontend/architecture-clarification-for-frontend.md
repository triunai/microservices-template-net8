# Backend Architecture Clarification for Frontend Developer

## 📋 Document Purpose

This document **clarifies and corrects** the original API specification based on our actual backend architecture. This is the **single source of truth** for frontend-backend integration.

**Original Spec:** `READMEs/From-Frontend/1st-request.md`  
**This Document:** Corrections, additions, and architectural context

---

## 🏗️ **System Architecture Overview**

### High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        FRONTEND (React)                         │
│  • Extracts tenant from subdomain/domain/user selection         │
│  • Manages JWT tokens (access + refresh)                        │
│  • Sends tenant context via query params or headers             │
└───────────────┬─────────────────────────────┬───────────────────┘
                │                             │
                │ OAuth Login                 │ API Requests
                │                             │
                v                             v
┌──────────────────────────┐   ┌─────────────────────────────────┐
│   SSO BROKER SERVICE     │   │    RGT.SPACE.API (This API)     │
│  (Separate Microservice) │   │   • Multi-tenant routing        │
│                          │   │   • RBAC permission engine      │
│  • Google OAuth          │   │   • Portal routing              │
│  • Microsoft OAuth       │   │   • Task allocation             │
│  • JWT minting (HS256)   │   │   • User management             │
│  • Token refresh         │   │   • Dashboard analytics         │
│  • JIT user provisioning │   │                                 │
└────────┬─────────────────┘   └────────┬────────────────────────┘
         │                              │
         │ Validates tenant             │ Validates JWT + queries DB
         │ Stores in rgt_auth_prototype │ Routes to tenant DB
         │                              │
         v                              v
┌──────────────────────────┐   ┌─────────────────────────────────┐
│  MASTER DB (PostgreSQL)  │   │   TENANT DBS (PostgreSQL)       │
│  • tenant_master         │   │   • client_a_db                 │
│    - tenants registry    │   │   • client_b_db                 │
│                          │   │   • rgt_internal_db             │
│  • rgt_auth_prototype    │   │                                 │
│    - users               │   │   Each contains:                │
│    - external_identities │   │   • users (tenant-scoped)       │
│    - refresh_tokens      │   │   • roles, permissions          │
│    - sso_configs         │   │   • clients, projects           │
│                          │   │   • project_assignments         │
│  • rgt_auth_audit        │   │   • audit logs                  │
│    - security_audit      │   │                                 │
└──────────────────────────┘   └─────────────────────────────────┘
```

---

## 🚨 **CRITICAL CORRECTIONS TO ORIGINAL SPEC**

### 1. OAuth Endpoints DO NOT Exist in RGT.Space.API

**❌ ORIGINAL SPEC (INCORRECT):**
```
GET /api/v1/auth/google/signin
GET /api/v1/auth/google/callback
GET /api/v1/auth/microsoft/signin
GET /api/v1/auth/microsoft/callback
```

**✅ CORRECTED:**
These endpoints exist in the **SSO Broker Service**, NOT in RGT.Space.API.

**SSO Broker Endpoints:**
```
GET  {SSO_BROKER_URL}/api/v1/auth/login?tenant={tenantCode}&provider={AzureEntra|Google}
GET  {SSO_BROKER_URL}/api/v1/auth/callback  (OAuth callback from IdP)
POST {SSO_BROKER_URL}/api/v1/auth/refresh
GET  {SSO_BROKER_URL}/api/v1/auth/logout
```

**Frontend OAuth Flow:**
1. User clicks "Sign in with Microsoft"
2. Frontend redirects to: `https://sso.rgtspace.com/api/v1/auth/login?tenant=7ELEVEN&provider=AzureEntra`
3. SSO Broker redirects to Microsoft OAuth consent screen
4. User authenticates with Microsoft
5. Microsoft redirects back to SSO Broker callback
6. SSO Broker mints JWT and redirects to: `https://app.rgtspace.com/auth/callback?token={JWT}&refreshToken={REFRESH_TOKEN}`
7. Frontend stores tokens and makes API calls to RGT.Space.API with `Authorization: Bearer {JWT}`

---

### 2. Local Email/Password Login

**RGT.Space.API DOES implement local authentication:**

**POST** `/api/v1/auth/login`

**Request:**
```typescript
{
  email: string;
  password: string;
  tenantCode: string;  // NEW: Required for tenant routing
}
```

**Success Response (200):**
```typescript
{
  isSuccess: true,
  value: {
    userId: string;              // UUID (not GUID in original spec)
    fullName: string;
    email: string;
    tenantCode: string;          // NEW: Tenant identifier
    token: string;               // JWT access token
    refreshToken: string;        // JWT refresh token
    refreshTokenExpiry: string;  // ISO 8601 datetime
  },
  errors: null
}
```

**Business Rules:**
- Validates against tenant-specific database (resolved from `tenantCode`)
- Password hash verification uses HMAC-SHA512 (matches schema: `password_hash` BYTEA)
- Checks `is_active` flag and `password_expiry_at`
- Stores session in tenant DB `user_sessions` table

---

### 3. JWT Claims Structure

**Actual JWT Claims (from SSO Broker):**
```json
{
  "sub": "018d1234-5678-7abc-def0-123456789abc",  // UUID v7 (userId)
  "email": "user@example.com",
  "name": "John Doe",
  "tid": "7ELEVEN",              // ⚠️ CRITICAL: Tenant identifier
  "ext_provider": "AzureEntra",   // OAuth provider (if SSO login)
  "ext_issuer": "https://login.microsoftonline.com/...",
  "ext_subject": "oid-from-azure-ad",
  "iss": "https://sso.rgtspace.com",
  "aud": "rgt-space-api",
  "iat": 1700000000,             // Issued at (Unix timestamp)
  "exp": 1700001200,             // Expires at (20 minutes)
  "jti": "unique-token-id"       // JWT ID
}
```

**For Local Login JWT (minted by RGT.Space.API):**
```json
{
  "sub": "018d1234-5678-7abc-def0-123456789abc",
  "email": "user@example.com",
  "name": "John Doe",
  "tid": "7ELEVEN",              // From login request
  "iss": "rgt-space-api",
  "aud": "rgt-space-api",
  "iat": 1700000000,
  "exp": 1700001200,
  "jti": "unique-token-id"
}
```

**⚠️ IMPORTANT:**
- No `role` in JWT - roles/permissions are queried from tenant DB
- `tid` claim is **mandatory** - used to route to correct tenant database
- Frontend must include tenant context in initial login (see Section 4)

---

### 4. Multi-Tenant Context Resolution

**How Tenant Context Works:**

#### **At Login Time:**
Frontend must provide tenant identifier via one of these methods:

**Option A: Extract from Subdomain**
```
https://7eleven.rgtspace.com → tenantCode = "7ELEVEN"
https://clienta.rgtspace.com → tenantCode = "CLIENTA"
```

**Option B: Extract from Email Domain**
```
user@7eleven.com → Lookup in master DB → tenantCode = "7ELEVEN"
user@rgt.com.my → Lookup in master DB → tenantCode = "RGT_INT"
```

**Option C: User Selection**
```html
<select name="tenant">
  <option value="7ELEVEN">7-Eleven Malaysia</option>
  <option value="RGT_INT">RGT Internal</option>
</select>
```

Frontend sends `tenantCode` in login request:
```typescript
POST /api/v1/auth/login
{
  email: "user@example.com",
  password: "password",
  tenantCode: "7ELEVEN"  // Extracted via A, B, or C
}
```

#### **For Authenticated Requests:**
Backend resolves tenant from (in order of precedence):

1. **JWT `tid` claim** (source of truth)
2. **`X-Tenant` header** (fallback if JWT parsing fails)
3. **`tenantId` query parameter** (backward compatibility)

**Example Authenticated Request:**
```http
GET /api/v1/dashboard/assignment-summary
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
X-Tenant: 7ELEVEN  (optional, JWT has this)
```

---

### 5. ID Type Correction: BIGINT → UUID

**❌ ORIGINAL SPEC:**
```typescript
{
  "id": "string"  // Comment: "GUID"
}
```

**✅ CORRECTED:**
All IDs are **UUID Version 7** (time-ordered UUIDs):
```typescript
{
  "id": "018d1234-5678-7abc-def0-123456789abc"  // UUID v7 (sortable by timestamp)
}
```

**Database Schema:**
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- ... other fields
);
```

**Benefits of UUID v7:**
- Globally unique (safe for distributed systems)
- Time-ordered (can ORDER BY id DESC for latest records)
- No auto-increment collisions in multi-tenant setup

**All ID fields affected:**
- `userId`, `clientId`, `projectId`, `roleId`, `moduleId`, `subModuleId`, `mappingId`, `taskPositionId`, etc.

---

### 6. Client-Project Relationship: 1:N → M:N

**❌ ORIGINAL SPEC ASSUMPTION:**
One project belongs to one client (1:N relationship)

**✅ CORRECTED SCHEMA:**
Many-to-many relationship via junction table

**Database Schema:**
```sql
-- Clients table (client directory)
CREATE TABLE clients (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL REFERENCES tenants(id),
    name        TEXT NOT NULL,
    logo_url    TEXT NULL,
    status      TEXT NOT NULL DEFAULT 'Active',
    -- audit fields
);

-- Projects table (project directory)
CREATE TABLE projects (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL REFERENCES tenants(id),
    name        TEXT NOT NULL,
    status      TEXT NOT NULL DEFAULT 'Active',
    -- NO client_id here
);

-- Junction table: Maps clients to projects with URLs
CREATE TABLE client_project_mappings (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL REFERENCES tenants(id),
    client_id   UUID NOT NULL REFERENCES clients(id),
    project_id  UUID NOT NULL REFERENCES projects(id),
    portal_url  TEXT NOT NULL,  -- URL is per mapping!
    status      TEXT NOT NULL DEFAULT 'Active',
    
    UNIQUE(tenant_id, client_id, project_id)
);
```

**Business Scenario:**
- **Project "ERP System"** exists once
- **Client A** accesses it at `https://clientA.portal.com/erp`
- **Client B** accesses it at `https://clientB.portal.com/erp`
- Same project, 2 mappings, 2 different URLs

**API Impact:**

**GET** `/api/v1/portal-routing/admin/mappings`

**Response:**
```typescript
{
  isSuccess: true,
  value: [
    {
      mappingId: "018d1234-...",     // Primary key of mapping
      clientId: "018d2345-...",
      clientName: "Client A",
      projectId: "018d3456-...",     // Same project ID
      projectName: "ERP System",
      portalUrl: "https://clientA.portal.com/erp",  // Different URL
      status: "Active"
    },
    {
      mappingId: "018d1235-...",     // Different mapping
      clientId: "018d2346-...",
      clientName: "Client B",
      projectId: "018d3456-...",     // Same project ID
      projectName: "ERP System",
      portalUrl: "https://clientB.portal.com/erp",  // Different URL
      status: "Active"
    }
  ]
}
```

---

### 7. Permission Model Mapping

**Database (Complex):**
```
Modules → Resources → Actions → Permissions → Role_Permissions → User_Roles
                                                                + User_Permission_Overrides
```

**API Response (Simple):**
```typescript
{
  modules: [{
    moduleName: "Portal Routing",
    subModules: [{
      subModuleName: "Admin Navigation Console",
      canView: true,
      canInsert: false,
      canEdit: true
    }]
  }]
}
```

**Mapping Rules:**

1. **Module → Module** (direct mapping)
2. **Resource → SubModule** (e.g., resource "AdminRoutingMappingTable" → submodule "Admin Navigation Console")
3. **Actions:** Exactly 3 seeded actions:
   - `VIEW` → `canView`
   - `INSERT` → `canInsert`
   - `EDIT` → `canEdit`

4. **Effective Permission Calculation:**
```csharp
// Backend calculates this
var rolePermissions = GetPermissionsFromUserRoles(userId, tenantId);
var allowOverrides = GetUserOverrides(userId, tenantId, isAllowed: true);
var denyOverrides = GetUserOverrides(userId, tenantId, isAllowed: false);

var effectivePermissions = rolePermissions
    .Union(allowOverrides)
    .Except(denyOverrides);

return new PermissionDto {
    SubModuleName = resource.Name,
    CanView = effectivePermissions.Contains("RESOURCE_VIEW"),
    CanInsert = effectivePermissions.Contains("RESOURCE_INSERT"),
    CanEdit = effectivePermissions.Contains("RESOURCE_EDIT")
};
```

**Frontend Never Sees:**
- `user_permission_overrides` table
- Permission codes like `PORTAL_ROUTING_ADMIN_VIEW`
- Internal RBAC complexity

---

## 🔄 **Token Refresh Flow**

**❌ ORIGINAL SPEC:**
```
POST /api/v1/auth/refresh  (in RGT.Space.API)
```

**✅ CORRECTED:**
Token refresh is handled by **SSO Broker**, not RGT.Space.API.

**Correct Flow:**

```mermaid
sequenceDiagram
    participant FE as Frontend
    participant API as RGT.Space.API
    participant SSO as SSO Broker
    
    FE->>API: GET /api/v1/users (with expired access token)
    API-->>FE: 401 Unauthorized
    FE->>SSO: POST /api/v1/auth/refresh { refreshToken }
    SSO->>SSO: Validate refresh token in rgt_auth_prototype DB
    SSO->>SSO: Mint new access token + refresh token
    SSO-->>FE: { token, refreshToken, expiresAt }
    FE->>API: GET /api/v1/users (with new access token)
    API-->>FE: 200 OK
```

**SSO Broker Refresh Endpoint:**
```
POST {SSO_BROKER_URL}/api/v1/auth/refresh
```

**Request:**
```typescript
{
  refreshToken: string;  // From initial login
}
```

**Response:**
```typescript
{
  isSuccess: true,
  value: {
    token: string;               // New JWT access token
    refreshToken: string;        // New refresh token (rotated)
    refreshTokenExpiry: string;  // ISO 8601
  },
  errors: null
}
```

**Business Rules:**
- Refresh token stored in `rgt_auth_prototype.refresh_tokens` (NOT tenant DB)
- Token rotation: Old refresh token invalidated after use
- Refresh token lifetime: 60 minutes (configurable)
- Access token lifetime: 20 minutes

**⚠️ RGT.Space.API does NOT implement `/api/v1/auth/refresh`**

---

## 🗄️ **Database Architecture**

### Master Databases (PostgreSQL)

**1. `tenant_master`**
```sql
CREATE TABLE tenants (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code                TEXT NOT NULL UNIQUE,  -- "7ELEVEN", "RGT_INT"
    name                TEXT NOT NULL,
    connection_string   TEXT NOT NULL,  -- Connection to tenant DB
    status              TEXT NOT NULL DEFAULT 'Active',
    -- audit fields
);
```

**2. `rgt_auth_prototype`** (SSO Broker DB)
```sql
-- Users (minimal profile for authentication)
CREATE TABLE users (
    user_id      UUID PRIMARY KEY,
    email        TEXT NOT NULL UNIQUE,
    display_name TEXT NULL
);

-- OAuth identity mappings
CREATE TABLE external_identities (
    identity_id     UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(user_id),
    provider        TEXT NOT NULL,  -- "AzureEntra", "Google"
    subject         TEXT NOT NULL,  -- IdP subject ID
    issuer          TEXT NOT NULL,
    UNIQUE(provider, subject)
);

-- Refresh tokens (NOT in tenant DBs)
CREATE TABLE refresh_tokens (
    token_id     UUID PRIMARY KEY,
    user_id      UUID NOT NULL REFERENCES users(user_id),
    tenant_code  TEXT NOT NULL,  -- Which tenant this session is for
    token_hash   TEXT NOT NULL,
    expires_at   TIMESTAMP NOT NULL,
    is_revoked   BOOLEAN DEFAULT FALSE,
    -- ... other fields
);
```

**3. `rgt_auth_audit`**
```sql
CREATE SCHEMA audit;

CREATE TABLE audit.security_audit (
    audit_id     UUID PRIMARY KEY,
    user_id      UUID NULL,
    tenant_code  TEXT NULL,
    event_type   TEXT NOT NULL,  -- "LOGIN", "LOGOUT", "TOKEN_REFRESH"
    ip_address   TEXT NULL,
    occurred_at  TIMESTAMP NOT NULL DEFAULT now()
);
```

### Tenant Databases

**Each tenant has a separate database** (e.g., `7eleven_db`, `rgt_internal_db`):

```
tenant_db/
├── Identity Layer
│   ├── users (tenant-scoped, NOT same as auth.users)
│   ├── user_sessions (for local login sessions)
│
├── RBAC Engine
│   ├── modules
│   ├── resources
│   ├── actions
│   ├── permissions
│   ├── roles
│   ├── user_roles
│   ├── role_permissions
│   ├── user_permission_overrides
│
├── Portal Routing
│   ├── clients
│   ├── projects
│   ├── client_project_mappings
│
├── Task Allocation
│   ├── position_types
│   ├── project_assignments
│
└── Audit
    ├── access_audit
    ├── grant_audit
```

**User Identity Duality:**

1. **SSO Broker User** (`rgt_auth_prototype.users`)
   - Minimal profile for authentication
   - Cross-tenant (can access multiple tenants)
   - OAuth identities linked here

2. **Tenant User** (`{tenant_db}.users`)
   - Full user profile (contact_number, role, permissions)
   - Tenant-scoped
   - Referenced by `project_assignments`, audit logs, etc.

**Linking:**
- Both share the same `user_id` (UUID)
- SSO broker creates user in `rgt_auth_prototype.users`
- RGT.Space.API creates/updates user in tenant DB on first login (JIT)

---

## 📝 **Corrected API Specifications**

### Authentication Module

#### **POST** `/api/v1/auth/login` (Local Email/Password)

**NEW: Added `tenantCode` field**

**Request:**
```typescript
{
  email: string;
  password: string;
  tenantCode: string;  // REQUIRED
}
```

**Response:** (UUID IDs)
```typescript
{
  isSuccess: true,
  value: {
    userId: string;              // UUID v7
    fullName: string;
    email: string;
    tenantCode: string;          // Echo back tenant
    token: string;
    refreshToken: string;
    refreshTokenExpiry: string;
  },
  errors: null
}
```

---

#### **❌ REMOVED:** OAuth Endpoints

These endpoints do NOT exist in RGT.Space.API:
- ~~GET /api/v1/auth/google/signin~~
- ~~GET /api/v1/auth/google/callback~~
- ~~GET /api/v1/auth/microsoft/signin~~
- ~~GET /api/v1/auth/microsoft/callback~~

**Use SSO Broker Instead:** `{SSO_BROKER_URL}/api/v1/auth/login?tenant={code}&provider={provider}`

---

#### **❌ REMOVED:** Refresh Token Endpoint

- ~~POST /api/v1/auth/refresh~~

**Use SSO Broker Instead:** `{SSO_BROKER_URL}/api/v1/auth/refresh`

---

#### **POST** `/api/v1/auth/forgot-password`

**Same as original spec** ✅

---

#### **POST** `/api/v1/auth/change-password`

**Same as original spec** ✅

---

#### **GET** `/api/v1/auth/permissions`

**CORRECTED: UUID IDs + Module Seeding**

**Response:**
```typescript
{
  isSuccess: true,
  value: {
    fullName: string;
    role: string;  // Role name from user_roles
    modules: [
      {
        moduleId: string;        // UUID (useful for frontend caching)
        moduleName: string;
        subModules: [
          {
            subModuleId: string;  // UUID (maps to resources.id)
            subModuleName: string;
            canView: boolean;     // Effective permission (includes overrides)
            canInsert: boolean;
            canEdit: boolean;
          }
        ]
      }
    ]
  },
  errors: null
}
```

**Seeded Modules (Guaranteed to Exist):**
```json
[
  {
    "moduleId": "...",
    "moduleName": "Portal Routing",
    "subModules": [
      { "subModuleId": "...", "subModuleName": "Client Navigation" },
      { "subModuleId": "...", "subModuleName": "Admin Navigation Console" }
    ]
  },
  {
    "moduleId": "...",
    "moduleName": "Task Allocation",
    "subModules": [
      { "subModuleId": "...", "subModuleName": "Member Task Distribution" },
      { "subModuleId": "...", "subModuleName": "Summary Allocation" }
    ]
  },
  {
    "moduleId": "...",
    "moduleName": "User Maintenance",
    "subModules": [
      { "subModuleId": "...", "subModuleName": "User Account" },
      { "subModuleId": "...", "subModuleName": "User Access Rights" }
    ]
  }
]
```

---

### Portal Routing Module

#### **GET** `/api/v1/portal-routing/admin/mappings`

**CORRECTED: Uses junction table, UUID IDs**

**Response:**
```typescript
{
  isSuccess: true,
  value: [
    {
      mappingId: string;      // UUID (client_project_mappings.id)
      clientId: string;       // UUID
      clientName: string;
      projectId: string;      // UUID
      projectName: string;
      portalUrl: string;      // From mappings table
      status: string;         // From mappings table
    }
  ]
}
```

**Note:** Multiple mappings can have the same `projectId` (M:N relationship)

---

#### **POST** `/api/v1/portal-routing/admin/mappings`

**CORRECTED: Creates mapping, not project**

**Request:**
```typescript
{
  clientId: string;    // UUID, must exist in clients table
  projectId: string;   // UUID, must exist in projects table
  portalUrl: string;   // Required
  status: string;      // "Active" | "Inactive"
}
```

**Business Rules:**
- Both client and project must already exist
- Unique constraint: `(tenant_id, client_id, project_id)`
- Cannot create duplicate mapping for same client-project pair

---

#### **PUT** `/api/v1/portal-routing/admin/mappings/{mappingId}`

**CORRECTED: Updates mapping, not project**

**Path:** `mappingId` is UUID from `client_project_mappings.id`

**Same request body as POST**

---

#### **NEW: Project Management Endpoints**

Since projects are now separate entities:

**GET** `/api/v1/portal-routing/projects`

**Response:**
```typescript
{
  isSuccess: true,
  value: [
    {
      projectId: string;   // UUID
      projectName: string;
      status: string;
    }
  ]
}
```

---

**POST** `/api/v1/portal-routing/projects`

**Request:**
```typescript
{
  projectName: string;
  status: string;  // "Active" | "Inactive"
}
```

**Response:**
```typescript
{
  isSuccess: true,
  value: {
    projectId: string;   // New UUID
    projectName: string;
    status: string;
  }
}
```

---

### Task Allocation Module

#### **GET** `/api/v1/task-allocation/project-mappings`

**CORRECTED: Uses denormalized position fields**

**Response Structure:**
```typescript
{
  isSuccess: true,
  value: [
    {
      assignmentGroupId: string;     // UUID (grouping multiple assignments for same project)
      projectId: string;             // UUID
      projectName: string;
      clientId: string;              // UUID (from first mapping if M:N)
      clientName: string;
      
      // Denormalized from project_assignments table
      technicalPICId: string | null;       // UUID
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
    }
  ]
}
```

**Backend Implementation:**
```csharp
// Pseudo-code: Pivot project_assignments rows into 6 columns
var assignments = await _dbContext.ProjectAssignments
    .Include(a => a.User)
    .Include(a => a.PositionType)
    .Include(a => a.Project)
    .Where(a => a.Project.TenantId == tenantId)
    .GroupBy(a => a.ProjectId)
    .Select(g => new ProjectMappingDto {
        ProjectId = g.Key,
        TechnicalPICId = g.FirstOrDefault(x => x.PositionType.Code == "TECH_PIC")?.UserId,
        TechnicalPICName = g.FirstOrDefault(x => x.PositionType.Code == "TECH_PIC")?.User.DisplayName,
        // ... repeat for other 5 positions
    });
```

**Seeded Position Types (Exact Codes):**
```sql
INSERT INTO position_types (id, name, code, sort_order) VALUES
('...', 'Technical PIC', 'TECH_PIC', 1),
('...', 'Technical Back-Up', 'TECH_BACKUP', 2),
('...', 'Functional PIC', 'FUNC_PIC', 3),
('...', 'Functional Back-Up', 'FUNC_BACKUP', 4),
('...', 'Support PIC', 'SUPPORT_PIC', 5),
('...', 'Support Back-Up', 'SUPPORT_BACKUP', 6);
```

---

#### **POST** `/api/v1/task-allocation/assignments`

**CORRECTED: Creates multiple rows in project_assignments**

**Request:**
```typescript
{
  projectId: string;           // UUID
  technicalPICId?: string;     // UUID (optional)
  technicalBackUpId?: string;
  functionalPICId?: string;
  functionalBackUpId?: string;
  supportPICId?: string;
  supportBackUpId?: string;
}
```

**Backend Logic:**
```csharp
// Create up to 6 rows in project_assignments
if (request.TechnicalPICId != null) {
    await _dbContext.ProjectAssignments.AddAsync(new ProjectAssignment {
        ProjectId = request.ProjectId,
        UserId = request.TechnicalPICId,
        PositionTypeId = TECH_PIC_POSITION_ID,
        AssignedAt = DateTime.UtcNow
    });
}
// Repeat for other 5 positions...
```

**Response:**
```typescript
{
  isSuccess: true,
  value: {
    assignmentGroupId: string;  // Generated ID for this project's assignments
    projectId: string;
    // ... full denormalized object
  }
}
```

---

#### **PUT** `/api/v1/task-allocation/assignments/{projectId}/positions/{positionType}`

**NEW: Update single position**

**Path Parameters:**
- `projectId` - UUID
- `positionType` - `"TECH_PIC" | "TECH_BACKUP" | "FUNC_PIC" | "FUNC_BACKUP" | "SUPPORT_PIC" | "SUPPORT_BACKUP"`

**Request:**
```typescript
{
  userId: string | null;  // UUID or null (null = unassign)
}
```

**Backend Logic:**
```csharp
var positionTypeId = GetPositionTypeId(positionType);

// Find existing assignment
var existing = await _dbContext.ProjectAssignments
    .FirstOrDefaultAsync(a => 
        a.ProjectId == projectId && 
        a.PositionTypeId == positionTypeId);

if (userId == null) {
    // Unassign: Delete row
    if (existing != null) {
        _dbContext.ProjectAssignments.Remove(existing);
    }
} else {
    if (existing != null) {
        // Update: Change user
        existing.UserId = userId;
    } else {
        // Insert: Create new assignment
        await _dbContext.ProjectAssignments.AddAsync(new ProjectAssignment {
            ProjectId = projectId,
            UserId = userId,
            PositionTypeId = positionTypeId
        });
    }
}
```

---

### User Management Module

**All endpoints same as original spec**, with these corrections:

1. **IDs are UUIDs** (not GUIDs in string format)
2. **`userRoleId` field:** Maps to `roles.id` (UUID)
3. **User creation:** Auto-generates UUID, not auto-increment

---

## 🔒 **Security & Validation**

### JWT Validation in RGT.Space.API

**Token Validation Middleware:**
```csharp
// Validates HS256 tokens from SSO Broker
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidIssuer = "https://sso.rgtspace.com",  // Or "rgt-space-api" for local tokens
            
            ValidateAudience = true,
            ValidAudience = "rgt-space-api",
            
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Configuration["Jwt:SigningKey"])
            ),
            
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero  // No grace period
        };
    });
```

**Shared Secret:**
- Both SSO Broker and RGT.Space.API use **same signing key**
- Stored in environment variable: `JWT_SIGNING_KEY`
- Algorithm: **HS256** (HMAC-SHA256)
- Key rotation strategy: TBD (future enhancement)

---

### Tenant Context Middleware

**Custom middleware to resolve tenant:**
```csharp
public class TenantResolutionMiddleware {
    public async Task InvokeAsync(HttpContext context) {
        string tenantCode = null;
        
        // 1. Try JWT claim (highest priority)
        if (context.User.Identity.IsAuthenticated) {
            tenantCode = context.User.FindFirst("tid")?.Value;
        }
        
        // 2. Fallback: X-Tenant header
        if (string.IsNullOrEmpty(tenantCode)) {
            context.Request.Headers.TryGetValue("X-Tenant", out var header);
            tenantCode = header.FirstOrDefault();
        }
        
        // 3. Fallback: Query parameter
        if (string.IsNullOrEmpty(tenantCode)) {
            tenantCode = context.Request.Query["tenantId"].FirstOrDefault();
        }
        
        if (string.IsNullOrEmpty(tenantCode)) {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new {
                isSuccess = false,
                errors = new[] { 
                    new { code = "TENANT_REQUIRED", message = "Tenant context is required" }
                }
            });
            return;
        }
        
        // Resolve tenant DB connection string from master DB
        var connectionString = await _tenantService.GetConnectionStringAsync(tenantCode);
        
        // Store in HttpContext for use by repositories
        context.Items["TenantConnectionString"] = connectionString;
        context.Items["TenantCode"] = tenantCode;
        
        await _next(context);
    }
}
```

---

### Permission Enforcement

**Attribute-based permission checking:**
```csharp
[HttpGet("admin/mappings")]
[RequirePermission("Portal Routing", "Admin Navigation Console", "VIEW")]
public async Task<IResult<List<MappingDto>>> GetMappings() {
    // Implementation
}
```

**Permission Check Logic:**
```csharp
public class RequirePermissionAttribute : TypeFilterAttribute {
    public RequirePermissionAttribute(string module, string subModule, string action)
        : base(typeof(PermissionFilter)) {
        Arguments = new[] { module, subModule, action };
    }
}

public class PermissionFilter : IAsyncActionFilter {
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ...) {
        var userId = context.HttpContext.User.FindFirst("sub")?.Value;
        var tenantCode = context.HttpContext.Items["TenantCode"] as string;
        
        var hasPermission = await _permissionService.CheckPermissionAsync(
            userId, tenantCode, module, subModule, action
        );
        
        if (!hasPermission) {
            context.Result = new JsonResult(new {
                isSuccess = false,
                errors = new[] { new { code = "FORBIDDEN", message = "Access denied" } }
            }) { StatusCode = 403 };
            return;
        }
        
        await next();
    }
}
```

**Effective Permission Service:**
```csharp
public async Task<bool> CheckPermissionAsync(
    string userId, string tenantCode, string module, string subModule, string action
) {
    // 1. Get permissions from roles
    var rolePermissions = await GetRolePermissionsAsync(userId, tenantCode, module, subModule, action);
    
    // 2. Get overrides
    var allowOverrides = await GetUserOverridesAsync(userId, tenantCode, module, subModule, action, isAllowed: true);
    var denyOverrides = await GetUserOverridesAsync(userId, tenantCode, module, subModule, action, isAllowed: false);
    
    // 3. Calculate: (RolePerms ∪ AllowOverrides) \ DenyOverrides
    var hasRolePermission = rolePermissions.Any();
    var hasAllowOverride = allowOverrides.Any();
    var hasDenyOverride = denyOverrides.Any();
    
    return (hasRolePermission || hasAllowOverride) && !hasDenyOverride;
}
```

---

## 📊 **Data Seeding Requirements**

**Backend must seed on first migration:**

### 1. Actions (Global Seed)
```sql
INSERT INTO actions (id, name, code) VALUES
('...', 'View', 'VIEW'),
('...', 'Insert', 'INSERT'),
('...', 'Edit', 'EDIT');
```

### 2. Modules & Resources (Per Tenant)
```sql
-- Module: Portal Routing
INSERT INTO modules (id, name, code) VALUES ('...', 'Portal Routing', 'PORTAL_ROUTING');

INSERT INTO resources (id, module_id, name, code) VALUES
('...', <portal_routing_id>, 'Client Navigation', 'CLIENT_NAV'),
('...', <portal_routing_id>, 'Admin Navigation Console', 'ADMIN_NAV');

-- Module: Task Allocation
INSERT INTO modules (id, name, code) VALUES ('...', 'Task Allocation', 'TASK_ALLOCATION');

INSERT INTO resources (id, module_id, name, code) VALUES
('...', <task_allocation_id>, 'Member Task Distribution', 'MEMBER_TASK'),
('...', <task_allocation_id>, 'Summary Allocation', 'SUMMARY_ALLOC');

-- Module: User Maintenance
INSERT INTO modules (id, name, code) VALUES ('...', 'User Maintenance', 'USER_MAINT');

INSERT INTO resources (id, module_id, name, code) VALUES
('...', <user_maint_id>, 'User Account', 'USER_ACCOUNT'),
('...', <user_maint_id>, 'User Access Rights', 'USER_ACCESS');
```

### 3. Permissions (Cross Product)
```sql
-- For each (resource, action) pair, create permission
INSERT INTO permissions (id, resource_id, action_id, code) VALUES
('...', <client_nav_resource>, <view_action>, 'CLIENT_NAV_VIEW'),
('...', <client_nav_resource>, <insert_action>, 'CLIENT_NAV_INSERT'),
('...', <client_nav_resource>, <edit_action>, 'CLIENT_NAV_EDIT'),
-- ... repeat for all 6 resources × 3 actions = 18 permissions
```

### 4. Position Types (Per Tenant)
```sql
INSERT INTO position_types (id, name, code, sort_order) VALUES
('...', 'Technical PIC', 'TECH_PIC', 1),
('...', 'Technical Back-Up', 'TECH_BACKUP', 2),
('...', 'Functional PIC', 'FUNC_PIC', 3),
('...', 'Functional Back-Up', 'FUNC_BACKUP', 4),
('...', 'Support PIC', 'SUPPORT_PIC', 5),
('...', 'Support Back-Up', 'SUPPORT_BACKUP', 6);
```

### 5. Default Admin Role (Per Tenant)
```sql
INSERT INTO roles (id, tenant_id, name, code, is_system) VALUES
('...', <tenant_id>, 'Administrator', 'ADMIN', true);

-- Grant all 18 permissions to admin role
INSERT INTO role_permissions (role_id, permission_id)
SELECT <admin_role_id>, id FROM permissions;
```

### 6. Default Admin User (Per Tenant)
```sql
INSERT INTO users (id, display_name, email, is_active, local_login_enabled, password_hash, password_salt) VALUES
('...', 'System Admin', 'admin@rgtspace.com', true, true, <hash>, <salt>);

INSERT INTO user_roles (user_id tenant_id, role_id) VALUES
(<admin_user_id>, <tenant_id>, <admin_role_id>);
```

**Password:** `Admin@123456789` (must be changed on first login)

---

## 🚀 **Frontend Integration Guide**

### 1. Environment Configuration

**Frontend `.env` file:**
```env
VITE_API_BASE_URL=https://api.rgtspace.com
VITE_SSO_BROKER_URL=https://sso.rgtspace.com
VITE_TENANT_CODE=7ELEVEN  # Or extract from subdomain
```

### 2. OAuth Login Flow

**Component: `LoginPage.tsx`**
```typescript
const handleOAuthLogin = (provider: 'AzureEntra' | 'Google') => {
  const tenantCode = extractTenantFromSubdomain(); // e.g., "7ELEVEN"
  const ssoUrl = `${import.meta.env.VITE_SSO_BROKER_URL}/api/v1/auth/login`;
  const params = new URLSearchParams({
    tenant: tenantCode,
    provider: provider
  });
  
  // Redirect to SSO Broker
  window.location.href = `${ssoUrl}?${params}`;
};
```

**Component: `AuthCallback.tsx`**
```typescript
// Handle redirect from SSO Broker: /?token=...&refreshToken=...
const searchParams = new URLSearchParams(window.location.search);
const token = searchParams.get('token');
const refreshToken = searchParams.get('refreshToken');

if (token && refreshToken) {
  // Store tokens
  localStorage.setItem('accessToken', token);
  localStorage.setItem('refreshToken', refreshToken);
  
  // Decode JWT to get user info
  const payload = JSON.parse(atob(token.split('.')[1]));
  
  // Redirect to dashboard
  navigate('/dashboard');
}
```

### 3. Local Login Flow

**Component: `LocalLoginForm.tsx`**
```typescript
const handleLocalLogin = async (email: string, password: string) => {
  const tenantCode = extractTenantFromSubdomain();
  
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, tenantCode })
  });
  
  const result = await response.json();
  
  if (result.isSuccess) {
    localStorage.setItem('accessToken', result.value.token);
    localStorage.setItem('refreshToken', result.value.refreshToken);
    navigate('/dashboard');
  } else {
    showError(result.errors[0].message);
  }
};
```

### 4. API Request Interceptor

**Axios Setup:**
```typescript
import axios from 'axios';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL
});

// Request interceptor: Add JWT token
apiClient.interceptors.request.use(config => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  
  // Optional: Add X-Tenant header (JWT already has tid claim)
  const tenantCode = getTenantCode();
  if (tenantCode) {
    config.headers['X-Tenant'] = tenantCode;
  }
  
  return config;
});

// Response interceptor: Handle 401 (token expired)
apiClient.interceptors.response.use(
  response => response,
  async error => {
    if (error.response?.status === 401) {
      // Token expired, try refresh
      const refreshToken = localStorage.getItem('refreshToken');
      
      try {
        const response = await fetch(`${SSO_BROKER_URL}/api/v1/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken })
        });
        
        const result = await response.json();
        
        if (result.isSuccess) {
          // Update tokens
          localStorage.setItem('accessToken', result.value.token);
          localStorage.setItem('refreshToken', result.value.refreshToken);
          
          // Retry original request
          error.config.headers.Authorization = `Bearer ${result.value.token}`;
          return axios.request(error.config);
        }
      } catch (refreshError) {
        // Refresh failed, redirect to login
        localStorage.clear();
        window.location.href = '/login';
      }
    }
    
    return Promise.reject(error);
  }
);
```

### 5. Tenant Extraction

**Utility: `getTenantCode.ts`**
```typescript
export function getTenantCode(): string {
  // Option A: From subdomain
  const hostname = window.location.hostname;  // "7eleven.rgtspace.com"
  const parts = hostname.split('.');
  if (parts.length >= 3) {
    return parts[0].toUpperCase();  // "7ELEVEN"
  }
  
  // Option B: From environment variable
  return import.meta.env.VITE_TENANT_CODE || 'RGT_INT';
}
```

---

## 📋 **Testing Checklist**

### Backend Must Provide:

- [ ] **Seed Script:** Seed all modules, resources, permissions, position types
- [ ] **Default Admin Account:** `admin@rgtspace.com` / `Admin@123456789`
- [ ] **Tenant DB Setup:** Script to create new tenant database
- [ ] **JWT Signing Key:** Shared with SSO Broker (document how to configure)
- [ ] **Postman Collection:** All endpoints with examples
- [ ] **Integration Tests:** Token validation, permission checks, multi-tenant isolation

### Frontend Must Test:

- [ ] **OAuth Login:** Google + Microsoft flows
- [ ] **Local Login:** Email/password with tenant
- [ ] **Token Refresh:** Automatic refresh on 401
- [ ] **Tenant Isolation:** Data from different tenants doesn't leak
- [ ] **Permission-based UI:** Buttons/pages hidden based on permissions
- [ ] **UUID Handling:** All IDs are valid UUID v7 format

---

## 🔧 **Configuration Requirements**

### Backend `appsettings.json`

```json
{
  "ConnectionStrings": {
    "MasterDb": "Host=localhost;Database=tenant_master;Username=postgres;Password=...",
    "AuthDb": "Host=localhost;Database=rgt_auth_prototype;Username=postgres;Password=..."
  },
  
  "Jwt": {
    "SigningKey": "SHARED-SECRET-KEY-WITH-SSO-BROKER",  // MUST match SSO Broker
    "Issuer": "rgt-space-api",
    "Audience": "rgt-space-api",
    "AccessTokenLifetimeMinutes": 20,
    "RefreshTokenLifetimeMinutes": 60
  },
  
  "SsoBroker": {
    "BaseUrl": "https://sso.rgtspace.com",
    "TrustedIssuers": ["https://sso.rgtspace.com", "rgt-space-api"]
  },
  
  "Tenant": {
    "DefaultTenantCode": "RGT_INT"
  }
}
```

### SSO Broker Configuration

**Must configure in SSO Broker:**
- Allowed redirect URIs: `https://app.rgtspace.com/auth/callback`
- Tenant registry: Sync with `tenant_master.tenants` table
- JWT signing key: Same as RGT.Space.API

---

## 📞 **Support & Questions**

**For Frontend Team:**
- Slack: `#backend-api-support`
- Email: `backend-team@rgtspace.com`

**API Documentation:**
- Swagger: `https://api.rgtspace.com/swagger`
- Postman: `https://postman.com/rgt-space-api`

**Breaking Changes:**
- All breaking changes will be announced 1 week in advance
- Version headers: `X-API-Version: 1.0`

---

## 📝 **Change Log**

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-26 | Initial clarification document |

---

**This document supersedes the original API specification for architectural decisions and data models.**

**Original spec remains valid for endpoint behavior unless explicitly corrected here.**
