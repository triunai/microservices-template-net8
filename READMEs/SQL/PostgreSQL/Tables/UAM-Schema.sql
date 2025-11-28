/* 
 * PROJECT: UAM & Resource Portal (Revamp)
 * DATABASE: PostgreSQL
 * 
 * NOTES:
 * 1. Server Timezone must be set to 'Asia/Kuala_Lumpur' in postgresql.conf.
 * 2. Uses UUID v7 for all Primary Keys (Time-ordered, index-friendly).
 * 3. Requires 'uuid_generate_v7()' function to be installed first.
 */

-- ==========================================
-- 1. TENANTS & USERS
-- ==========================================

-- 1.1 TENANTS
CREATE TABLE tenants (
    id                UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name              TEXT        NOT NULL,
    code              TEXT        NOT NULL,          -- e.g. "RGT_INT", "CLIENT_X"
    status            TEXT        NOT NULL DEFAULT 'Active',
    connection_string TEXT        NOT NULL,          -- Added to match Master DB requirements

    created_at        TIMESTAMP   NOT NULL DEFAULT now(),
    created_by        UUID        NULL, 
    updated_at        TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by        UUID        NULL, 
    is_deleted        BOOLEAN     NOT NULL DEFAULT FALSE,
    deleted_at        TIMESTAMP   NULL,
    deleted_by        UUID        NULL,

    CONSTRAINT tenants_status_chk CHECK (status IN ('Active', 'Suspended', 'Archived')),
    CONSTRAINT tenants_code_uk UNIQUE (code)
);

-- 1.2 USERS
CREATE TABLE users (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),

    -- PROFILE
    display_name            TEXT        NOT NULL,
    email                   TEXT        NOT NULL,
    contact_number          TEXT        NULL,
    is_active               BOOLEAN     NOT NULL DEFAULT TRUE,

    -- LOCAL AUTH
    local_login_enabled     BOOLEAN     NOT NULL DEFAULT TRUE,
    password_hash           BYTEA       NULL,
    password_salt           BYTEA       NULL,
    password_last_changed_at TIMESTAMP  NULL,
    password_expiry_at      TIMESTAMP   NULL,
    password_reset_token    TEXT        NULL,
    password_reset_expires_at TIMESTAMP NULL,

    -- SSO AUTH
    sso_login_enabled       BOOLEAN     NOT NULL DEFAULT FALSE,
    sso_provider            TEXT        NULL,   -- 'azuread', 'google'
    external_id             TEXT        NULL,   -- provider subject / objectId
    sso_email               TEXT        NULL,
    last_login_at           TIMESTAMP   NULL,
    last_login_provider     TEXT        NULL,

    -- AUDIT / LIFECYCLE
    created_at              TIMESTAMP   NOT NULL DEFAULT now(),
    created_by              UUID        NULL REFERENCES users (id),
    updated_at              TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by              UUID        NULL REFERENCES users (id),
    is_deleted              BOOLEAN     NOT NULL DEFAULT FALSE,
    deleted_at              TIMESTAMP   NULL,
    deleted_by              UUID        NULL REFERENCES users (id),

    CONSTRAINT users_email_uk UNIQUE (email),
    CONSTRAINT users_sso_uk   UNIQUE (sso_provider, external_id)
);

-- 1.3 USER SESSIONS
CREATE TABLE user_sessions (
    id              UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id         UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    
    refresh_token   TEXT        NOT NULL,
    expires_at      TIMESTAMP   NOT NULL,
    
    -- Security Context
    created_at      TIMESTAMP   NOT NULL DEFAULT now(),
    created_ip      TEXT        NULL,
    device_info     TEXT        NULL, 
    
    is_revoked      BOOLEAN     NOT NULL DEFAULT FALSE,
    revoked_at      TIMESTAMP   NULL,
    replaced_by     TEXT        NULL, 

    CONSTRAINT user_sessions_token_uk UNIQUE (refresh_token)
);

-- Add FKs for Tenants after Users table exists
ALTER TABLE tenants ADD CONSTRAINT fk_tenants_created_by FOREIGN KEY (created_by) REFERENCES users(id);
ALTER TABLE tenants ADD CONSTRAINT fk_tenants_updated_by FOREIGN KEY (updated_by) REFERENCES users(id);
ALTER TABLE tenants ADD CONSTRAINT fk_tenants_deleted_by FOREIGN KEY (deleted_by) REFERENCES users(id);

-- ==========================================
-- 2. MODULES, RESOURCES, ACTIONS
-- ==========================================

-- 2.1 MODULES
CREATE TABLE modules (
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,   -- e.g. "USER_MAINTENANCE"
    is_active  BOOLEAN     NOT NULL DEFAULT TRUE,
    sort_order INT         NULL,

    created_at TIMESTAMP   NOT NULL DEFAULT now(),
    created_by UUID        NULL REFERENCES users (id),
    updated_at TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by UUID        NULL REFERENCES users (id),
    is_deleted BOOLEAN     NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP   NULL,
    deleted_by UUID        NULL REFERENCES users (id),

    CONSTRAINT modules_code_uk UNIQUE (code)
);

-- 2.2 RESOURCES
CREATE TABLE resources (
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    module_id  UUID        NOT NULL REFERENCES modules (id),
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,

    created_at TIMESTAMP   NOT NULL DEFAULT now(),
    created_by UUID        NULL REFERENCES users (id),
    updated_at TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by UUID        NULL REFERENCES users (id),
    is_deleted BOOLEAN     NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP   NULL,
    deleted_by UUID        NULL REFERENCES users (id),

    CONSTRAINT resources_module_code_uk UNIQUE (module_id, code)
);

-- 2.3 ACTIONS
CREATE TABLE actions (
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,

    created_at TIMESTAMP   NOT NULL DEFAULT now(),
    created_by UUID        NULL REFERENCES users (id),
    updated_at TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by UUID        NULL REFERENCES users (id),

    CONSTRAINT actions_code_uk UNIQUE (code)
);

-- 2.4 PERMISSIONS
CREATE TABLE permissions (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    resource_id UUID        NOT NULL REFERENCES resources (id),
    action_id   UUID        NOT NULL REFERENCES actions (id),
    code        TEXT        NOT NULL,   -- e.g. "PORTAL_EDIT"
    description TEXT        NULL,

    created_at  TIMESTAMP   NOT NULL DEFAULT now(),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by  UUID        NULL REFERENCES users (id),

    CONSTRAINT permissions_resource_action_uk UNIQUE (resource_id, action_id),
    CONSTRAINT permissions_code_uk            UNIQUE (code)
);

-- ==========================================
-- 3. PORTAL ROUTING (Clients & Projects)
-- ==========================================

-- 3.1 CLIENTS
CREATE TABLE clients (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name        TEXT        NOT NULL,
    logo_url    TEXT        NULL,
    status      TEXT        NOT NULL DEFAULT 'Active',
    
    created_at  TIMESTAMP   NOT NULL DEFAULT now(),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by  UUID        NULL REFERENCES users (id),
    is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE
);

-- 3.2 PROJECTS
CREATE TABLE projects (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name        TEXT        NOT NULL,
    status      TEXT        NOT NULL DEFAULT 'Active',

    created_at  TIMESTAMP   NOT NULL DEFAULT now(),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by  UUID        NULL REFERENCES users (id),
    is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE
);

-- 3.3 CLIENT-PROJECT MAPPINGS (M:N Junction Table)
CREATE TABLE client_project_mappings (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    client_id   UUID        NOT NULL REFERENCES clients (id),
    project_id  UUID        NOT NULL REFERENCES projects (id),
    portal_url  TEXT        NOT NULL,
    status      TEXT        NOT NULL DEFAULT 'Active',
    
    created_at  TIMESTAMP   NOT NULL DEFAULT now(),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by  UUID        NULL REFERENCES users (id),
    is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE,

    CONSTRAINT client_project_mappings_uk UNIQUE (client_id, project_id)
);

-- ==========================================
-- 4. TASK ALLOCATION (Resource Mgmt)
-- ==========================================

-- 4.1 POSITION TYPES
CREATE TABLE position_types (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name        TEXT        NOT NULL, -- e.g. "Technical PIC"
    code        TEXT        NOT NULL, -- e.g. "TECH_PIC"
    sort_order  INT         NOT NULL DEFAULT 0,
    
    CONSTRAINT position_types_code_uk UNIQUE (code)
);

-- 4.2 PROJECT ASSIGNMENTS
CREATE TABLE project_assignments (
    id               UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    project_id       UUID        NOT NULL REFERENCES projects (id),
    user_id          UUID        NOT NULL REFERENCES users (id),
    position_type_id UUID        NOT NULL REFERENCES position_types (id),
    
    assigned_at      TIMESTAMP   NOT NULL DEFAULT now(),
    assigned_by      UUID        NULL REFERENCES users (id),

    -- Business Logic: A user cannot hold the exact same position on the same project twice.
    CONSTRAINT project_assignments_uk UNIQUE (project_id, user_id, position_type_id)
);

-- ==========================================
-- 5. ROLES & GROUPS
-- ==========================================

-- 5.1 ROLES
CREATE TABLE roles (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    tenant_id   UUID        NULL REFERENCES tenants (id), -- NULL = global role
    name        TEXT        NOT NULL,
    code        TEXT        NOT NULL,
    description TEXT        NULL,
    is_system   BOOLEAN     NOT NULL DEFAULT FALSE,
    is_active   BOOLEAN     NOT NULL DEFAULT TRUE,

    created_at  TIMESTAMP   NOT NULL DEFAULT now(),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by  UUID        NULL REFERENCES users (id),

    CONSTRAINT roles_tenant_code_uk UNIQUE (tenant_id, code),
    CONSTRAINT roles_tenant_id_id_uk UNIQUE (tenant_id, id) -- Composite key support
);

-- 5.2 ROLE PERMISSIONS
CREATE TABLE role_permissions (
    role_id       UUID NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions (id) ON DELETE CASCADE,

    CONSTRAINT role_permissions_pk PRIMARY KEY (role_id, permission_id)
);

-- 5.3 GROUPS
CREATE TABLE groups (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    tenant_id   UUID        NOT NULL REFERENCES tenants (id),
    name        TEXT        NOT NULL,
    description TEXT        NULL,

    created_at  TIMESTAMP   NOT NULL DEFAULT now(),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by  UUID        NULL REFERENCES users (id),
    is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE,
    deleted_at  TIMESTAMP   NULL,
    deleted_by  UUID        NULL REFERENCES users (id),

    CONSTRAINT groups_tenant_name_uk UNIQUE (tenant_id, name)
);

-- 5.4 GROUP MEMBERS
CREATE TABLE group_members (
    group_id UUID NOT NULL REFERENCES groups (id) ON DELETE CASCADE,
    user_id  UUID NOT NULL REFERENCES users  (id) ON DELETE CASCADE,

    CONSTRAINT group_members_pk PRIMARY KEY (group_id, user_id)
);

-- 5.5 USER ROLES
CREATE TABLE user_roles (
    id                  UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id             UUID        NOT NULL REFERENCES users   (id),
    tenant_id           UUID        NOT NULL REFERENCES tenants (id),
    role_id             UUID        NOT NULL REFERENCES roles   (id),
    assigned_by_user_id UUID        NULL REFERENCES users (id),
    assigned_at         TIMESTAMP   NOT NULL DEFAULT now(),

    CONSTRAINT user_roles_uk UNIQUE (user_id, tenant_id, role_id),
    CONSTRAINT user_roles_tenant_role_fk FOREIGN KEY (tenant_id, role_id) REFERENCES roles (tenant_id, id)
);

-- 5.6 GROUP ROLES
CREATE TABLE group_roles (
    id                  UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    group_id            UUID        NOT NULL REFERENCES groups (id) ON DELETE CASCADE,
    role_id             UUID        NOT NULL REFERENCES roles  (id),
    assigned_by_user_id UUID        NULL REFERENCES users (id),
    assigned_at         TIMESTAMP   NOT NULL DEFAULT now(),

    CONSTRAINT group_roles_uk UNIQUE (group_id, role_id)
);

-- ==========================================
-- 6. AUDIT & OVERRIDES
-- ==========================================

-- 6.1 ACCESS AUDIT
CREATE TABLE access_audit (
    id            UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id       UUID        NULL REFERENCES users   (id),
    tenant_id     UUID        NULL REFERENCES tenants (id),
    permission_id UUID        NULL REFERENCES permissions (id),

    action        TEXT        NOT NULL,
    resource_type TEXT        NOT NULL,
    resource_id   TEXT        NOT NULL,
    context       JSONB       NULL,

    occurred_at   TIMESTAMP   NOT NULL DEFAULT now()
);

-- 6.2 GRANT AUDIT
CREATE TABLE grant_audit (
    id              UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    actor_user_id   UUID        NULL REFERENCES users   (id),
    target_user_id  UUID        NULL REFERENCES users   (id),
    tenant_id       UUID        NULL REFERENCES tenants (id),
    role_id         UUID        NULL REFERENCES roles   (id),

    grant_type      TEXT        NOT NULL,
    reason          TEXT        NULL,
    occurred_at     TIMESTAMP   NOT NULL DEFAULT now()
);

-- 6.3 USER PERMISSION OVERRIDES
CREATE TABLE user_permission_overrides (
    id            UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id       UUID        NOT NULL REFERENCES users   (id),
    tenant_id     UUID        NOT NULL REFERENCES tenants (id),
    permission_id UUID        NOT NULL REFERENCES permissions (id),

    is_allowed    BOOLEAN     NOT NULL, -- TRUE = Grant, FALSE = Deny

    reason        TEXT        NULL,
    created_at    TIMESTAMP   NOT NULL DEFAULT now(),
    created_by    UUID        NULL REFERENCES users (id),

    CONSTRAINT user_permission_overrides_uk UNIQUE (user_id, tenant_id, permission_id)
);

-- ==========================================
-- 7. INDEXING
-- ==========================================

-- Users & Auth
CREATE INDEX idx_users_email        ON users (email);
CREATE INDEX idx_users_external_id  ON users (sso_provider, external_id);
CREATE INDEX idx_users_is_active    ON users (is_active);
CREATE INDEX idx_user_sessions_uid  ON user_sessions (user_id);
CREATE INDEX idx_user_sessions_tkn  ON user_sessions (refresh_token);

-- RBAC
CREATE INDEX idx_user_roles_user_tenant ON user_roles (user_id, tenant_id);
CREATE INDEX idx_user_roles_role        ON user_roles (role_id);
CREATE INDEX idx_group_members_user     ON group_members (user_id);
CREATE INDEX idx_role_permissions_perm  ON role_permissions (permission_id);
CREATE INDEX idx_user_perm_overrides    ON user_permission_overrides (user_id, tenant_id);

-- Portal & Task Alloc
CREATE INDEX idx_projects_client        ON projects (client_id);
CREATE INDEX idx_proj_assignments_proj  ON project_assignments (project_id);
CREATE INDEX idx_proj_assignments_user  ON project_assignments (user_id);

-- Audit
CREATE INDEX idx_access_audit_tenant_time ON access_audit (tenant_id, occurred_at DESC);
CREATE INDEX idx_access_audit_user_time   ON access_audit (user_id,  occurred_at DESC);
CREATE INDEX idx_grant_audit_target_time  ON grant_audit  (target_user_id, occurred_at DESC);
