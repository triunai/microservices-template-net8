You are sharp to catch that. **No, this SQL does NOT fully align with the "Internal Tool" / Global Roles architecture.**

It is currently in a "Hybrid State" that is confusing and dangerous.

### 🚨 The Conflict
1.  **The SQL allows Global Roles definition:**
    *   `roles.tenant_id` is `NULL`able. So yes, you *can* create a role called "SuperAdmin" that isn't tied to a tenant.
2.  **BUT... The SQL forces Tenant-Scoped ASSIGNMENT:**
    *   Look at `user_roles`: `tenant_id UUID NOT NULL REFERENCES tenants (id)`.
    *   **The Problem:** You cannot assign that "Global SuperAdmin" role to a user without **also** picking a `tenant_id`.
    *   This defeats the purpose. You'd have to assign "SuperAdmin" to User A for *every single tenant* individually.

### 🛠️ The Fix: "De-Tenant" the RBAC
Since this is an **Internal Tool** (RGT Space managing Clients), we need to rip `tenant_id` out of the Security/RBAC tables entirely.

Here is the **Corrected, Global-First Schema** for your UAM.

#### Key Changes:
1.  **Deleted `tenants` table:** We don't need it. We have `clients` for the business data.
2.  **Global Roles:** Removed `tenant_id` from `roles`. All roles apply to the whole system.
3.  **Global Assignments:** Removed `tenant_id` from `user_roles`. You are just an "Admin", period.
4.  **Global Overrides:** Removed `tenant_id` from `overrides`.

---

### ✅ The Corrected SQL (Copy this)

```sql
/* 
 * PROJECT: ReactPortal (Internal Tool Architecture)
 * CONTEXT: Single Database, Global RBAC, Client Resource Management
 */

-- ==========================================
-- 1. IDENTITY (Global)
-- ==========================================

-- 1.1 USERS (No changes needed, just removed tenant references if any)
CREATE TABLE users (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    display_name            TEXT        NOT NULL,
    email                   TEXT        NOT NULL,
    is_active               BOOLEAN     NOT NULL DEFAULT TRUE,
    
    -- Auth Fields
    password_hash           BYTEA       NULL,
    sso_provider            TEXT        NULL,
    external_id             TEXT        NULL,
    
    -- Audit
    created_at              TIMESTAMP   NOT NULL DEFAULT now(),
    updated_at              TIMESTAMP   NOT NULL DEFAULT now(),
    is_deleted              BOOLEAN     NOT NULL DEFAULT FALSE,

    CONSTRAINT users_email_uk UNIQUE (email)
);

-- 1.2 USER SESSIONS (Kept as is - strictly Global)
CREATE TABLE user_sessions (
    id              UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id         UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    refresh_token   TEXT        NOT NULL,
    expires_at      TIMESTAMP   NOT NULL,
    is_revoked      BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMP   NOT NULL DEFAULT now()
);

-- ==========================================
-- 2. GLOBAL RBAC (The Fix)
-- ==========================================

-- 2.1 ROLES (REMOVED tenant_id)
CREATE TABLE roles (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name        TEXT        NOT NULL,   -- e.g. "System Admin", "Project Manager"
    code        TEXT        NOT NULL,   -- e.g. "SYS_ADMIN"
    description TEXT        NULL,
    is_system   BOOLEAN     NOT NULL DEFAULT FALSE, -- Cannot be deleted
    
    CONSTRAINT roles_code_uk UNIQUE (code)
);

-- 2.2 PERMISSIONS (Kept as is, but conceptually Global)
CREATE TABLE permissions (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    code        TEXT        NOT NULL,   -- e.g. "PROJECTS_VIEW"
    description TEXT        NULL,
    CONSTRAINT permissions_code_uk UNIQUE (code)
);

-- 2.3 ROLE PERMISSIONS (Junction)
CREATE TABLE role_permissions (
    role_id       UUID NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions (id) ON DELETE CASCADE,
    CONSTRAINT role_permissions_pk PRIMARY KEY (role_id, permission_id)
);

-- 2.4 USER ROLES (REMOVED tenant_id - Pure Global Assignment)
CREATE TABLE user_roles (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id     UUID        NOT NULL REFERENCES users (id),
    role_id     UUID        NOT NULL REFERENCES roles (id),
    assigned_at TIMESTAMP   NOT NULL DEFAULT now(),

    CONSTRAINT user_roles_uk UNIQUE (user_id, role_id)
);

-- 2.5 USER PERMISSION OVERRIDES (REMOVED tenant_id)
CREATE TABLE user_permission_overrides (
    id            UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id       UUID        NOT NULL REFERENCES users (id),
    permission_id UUID        NOT NULL REFERENCES permissions (id),
    is_allowed    BOOLEAN     NOT NULL, -- Break-glass mechanism
    reason        TEXT        NULL,
    
    CONSTRAINT user_permission_overrides_uk UNIQUE (user_id, permission_id)
);

-- ==========================================
-- 3. BUSINESS DOMAIN (Clients & Projects)
-- ==========================================

-- 3.1 CLIENTS (These are RESOURCES, not Tenants)
CREATE TABLE clients (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name        TEXT        NOT NULL,
    code        TEXT        NOT NULL, -- e.g. "TOYOTA"
    logo_url    TEXT        NULL,
    is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE
);

-- 3.2 PROJECTS (Must belong to a Client)
CREATE TABLE projects (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    client_id   UUID        NOT NULL REFERENCES clients (id),
    name        TEXT        NOT NULL,
    status      TEXT        NOT NULL DEFAULT 'Active',
    is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE
);

-- 3.3 MAPPINGS (Routing)
CREATE TABLE client_project_mappings (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    client_id   UUID        NOT NULL REFERENCES clients (id),
    project_id  UUID        NOT NULL REFERENCES projects (id),
    portal_url  TEXT        NOT NULL,
    
    CONSTRAINT client_project_mappings_uk UNIQUE (client_id, project_id)
);

-- ==========================================
-- 4. RESOURCE ALLOCATION
-- ==========================================

-- 4.1 POSITION TYPES
CREATE TABLE position_types (
    id      UUID    PRIMARY KEY DEFAULT uuid_generate_v7(),
    code    TEXT    NOT NULL UNIQUE, -- TECH_PIC
    name    TEXT    NOT NULL
);

-- 4.2 ASSIGNMENTS (Fixed the UNIQUE constraint)
CREATE TABLE project_assignments (
    id               UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    project_id       UUID        NOT NULL REFERENCES projects (id),
    user_id          UUID        NOT NULL REFERENCES users (id),
    position_type_id UUID        NOT NULL REFERENCES position_types (id),

    -- UPDATED LOGIC: 
    -- 1. A User cannot have the SAME position on the SAME project twice.
    -- 2. But multiple users can hold the same position (e.g. 2 Support PICs).
    CONSTRAINT project_assignments_uk UNIQUE (project_id, user_id, position_type_id)
);
```

### 🧠 Why this is better
1.  **Simplicity:** No `tenant_id` confusion.
2.  **Cohesion:** Admin roles give you power over the *System*, which includes managing *all* Clients.
3.  **Flexibility:** You can assign "User A" as "Technical Lead" on a Toyota project (via `project_assignments`) without needing complex tenant-based RBAC roles. The RBAC controls "Can I edit projects?", the Assignments control "Am I working on this project?".