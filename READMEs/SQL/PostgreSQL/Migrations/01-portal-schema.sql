-- ==========================================
-- 1. PORTAL DB SCHEMA (rgt_space_portal_db)
-- ==========================================
-- Target Database: rgt_space_portal_db
-- Dependencies: 00-extensions.sql (uuid_generate_v7)

-- 1.1 USERS & SESSIONS
-- Note: Local user store for the portal. SSO users will be synced/linked here.

CREATE TABLE users (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- Profile
    display_name            TEXT        NOT NULL,
    email                   TEXT        NOT NULL,
    contact_number          TEXT        NULL,
    is_active               BOOLEAN     NOT NULL DEFAULT TRUE,
    
    -- Local Auth
    local_login_enabled     BOOLEAN     NOT NULL DEFAULT TRUE,
    password_hash           BYTEA       NULL,
    password_salt           BYTEA       NULL,
    password_last_changed_at TIMESTAMP  NULL,
    password_expiry_at      TIMESTAMP   NULL,
    password_reset_token    TEXT        NULL,
    password_reset_expires_at TIMESTAMP NULL,
    
    -- SSO Integration
    sso_login_enabled       BOOLEAN     NOT NULL DEFAULT FALSE,
    sso_provider            TEXT        NULL,   -- e.g., 'azuread'
    external_id             TEXT        NULL,   -- The 'sub' from the SSO provider
    sso_email               TEXT        NULL,
    last_login_at           TIMESTAMP   NULL,
    last_login_provider     TEXT        NULL,
    
    -- Audit
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

CREATE TABLE user_sessions (
    id              UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id         UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    
    refresh_token   TEXT        NOT NULL,
    expires_at      TIMESTAMP   NOT NULL,
    
    created_at      TIMESTAMP   NOT NULL DEFAULT now(),
    created_ip      TEXT        NULL,
    device_info     TEXT        NULL,
    
    is_revoked      BOOLEAN     NOT NULL DEFAULT FALSE,
    revoked_at      TIMESTAMP   NULL,
    replaced_by     TEXT        NULL,

    CONSTRAINT user_sessions_token_uk UNIQUE (refresh_token)
);

-- 1.2 RBAC CORE (Modules -> Resources -> Actions -> Permissions)

CREATE TABLE modules (
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,   -- e.g. "PORTAL_ROUTING"
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

CREATE TABLE resources (
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    module_id  UUID        NOT NULL REFERENCES modules (id),
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,   -- e.g. "CLIENT_NAV"
    
    created_at TIMESTAMP   NOT NULL DEFAULT now(),
    created_by UUID        NULL REFERENCES users (id),
    updated_at TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by UUID        NULL REFERENCES users (id),
    is_deleted BOOLEAN     NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP   NULL,
    deleted_by UUID        NULL REFERENCES users (id),

    CONSTRAINT resources_module_code_uk UNIQUE (module_id, code)
);

CREATE TABLE actions (
    id         UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name       TEXT        NOT NULL,
    code       TEXT        NOT NULL,   -- e.g. "VIEW", "EDIT"
    
    created_at TIMESTAMP   NOT NULL DEFAULT now(),
    created_by UUID        NULL REFERENCES users (id),
    updated_at TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by UUID        NULL REFERENCES users (id),

    CONSTRAINT actions_code_uk UNIQUE (code)
);

CREATE TABLE permissions (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    resource_id UUID        NOT NULL REFERENCES resources (id),
    action_id   UUID        NOT NULL REFERENCES actions (id),
    code        TEXT        NOT NULL,   -- e.g. "CLIENT_NAV_VIEW"
    description TEXT        NULL,
    
    created_at  TIMESTAMP   NOT NULL DEFAULT now(),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by  UUID        NULL REFERENCES users (id),

    CONSTRAINT permissions_resource_action_uk UNIQUE (resource_id, action_id),
    CONSTRAINT permissions_code_uk            UNIQUE (code)
);

-- 1.3 ROLES & ASSIGNMENTS

CREATE TABLE roles (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    -- Removed tenant_id as this is a single-tenant internal tool DB
    name        TEXT        NOT NULL,
    code        TEXT        NOT NULL,
    description TEXT        NULL,
    is_system   BOOLEAN     NOT NULL DEFAULT FALSE,
    is_active   BOOLEAN     NOT NULL DEFAULT TRUE,
    
    created_at  TIMESTAMP   NOT NULL DEFAULT now(),
    created_by  UUID        NULL REFERENCES users (id),
    updated_at  TIMESTAMP   NOT NULL DEFAULT now(),
    updated_by  UUID        NULL REFERENCES users (id),

    CONSTRAINT roles_code_uk UNIQUE (code)
);

CREATE TABLE role_permissions (
    role_id       UUID NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions (id) ON DELETE CASCADE,
    CONSTRAINT role_permissions_pk PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE user_roles (
    id                  UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id             UUID        NOT NULL REFERENCES users   (id),
    role_id             UUID        NOT NULL REFERENCES roles   (id),
    assigned_by_user_id UUID        NULL REFERENCES users (id),
    assigned_at         TIMESTAMP   NOT NULL DEFAULT now(),

    CONSTRAINT user_roles_uk UNIQUE (user_id, role_id)
);

CREATE TABLE user_permission_overrides (
    id            UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id       UUID        NOT NULL REFERENCES users   (id),
    permission_id UUID        NOT NULL REFERENCES permissions (id),
    is_allowed    BOOLEAN     NOT NULL, -- TRUE = Grant, FALSE = Deny
    reason        TEXT        NULL,
    
    created_at    TIMESTAMP   NOT NULL DEFAULT now(),
    created_by    UUID        NULL REFERENCES users (id),

    CONSTRAINT user_permission_overrides_uk UNIQUE (user_id, permission_id)
);

-- 1.4 PORTAL ROUTING (Clients & Projects)

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

-- 1.5 TASK ALLOCATION

CREATE TABLE position_types (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    name        TEXT        NOT NULL,
    code        TEXT        NOT NULL UNIQUE,
    sort_order  INT         NOT NULL DEFAULT 0
);

CREATE TABLE project_assignments (
    id               UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    project_id       UUID        NOT NULL REFERENCES projects (id),
    user_id          UUID        NOT NULL REFERENCES users (id),
    position_type_id UUID        NOT NULL REFERENCES position_types (id),
    
    assigned_at      TIMESTAMP   NOT NULL DEFAULT now(),
    assigned_by      UUID        NULL REFERENCES users (id),

    CONSTRAINT project_assignments_uk UNIQUE (project_id, user_id, position_type_id)
);

-- 1.6 AUDIT LOG (Local)

CREATE TABLE audit_log (
    id                UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id           TEXT        NULL,
    client_id         TEXT        NULL,
    ip_address        VARCHAR(50) NULL,
    user_agent        TEXT        NULL,
    action            VARCHAR(100) NOT NULL,
    entity_type       VARCHAR(100) NULL,
    entity_id         VARCHAR(100) NULL,
    timestamp         TIMESTAMPTZ NOT NULL DEFAULT now(),
    correlation_id    VARCHAR(100) NULL,
    request_path      VARCHAR(500) NULL,
    is_success        BOOLEAN     NOT NULL,
    status_code       INTEGER     NULL,
    error_code        VARCHAR(50) NULL,
    error_message     TEXT        NULL,
    duration_ms       INTEGER     NULL,
    request_data      BYTEA       NULL,
    response_data     BYTEA       NULL,
    delta             BYTEA       NULL,
    idempotency_key   VARCHAR(100) NULL,
    source            VARCHAR(50) NOT NULL DEFAULT 'API',
    request_hash      VARCHAR(64) NULL
);

CREATE INDEX idx_audit_timestamp ON audit_log(timestamp DESC);
CREATE INDEX idx_audit_user_timestamp ON audit_log(user_id, timestamp DESC);
CREATE INDEX idx_audit_correlation ON audit_log(correlation_id);

-- 1.7 INDEXES
CREATE INDEX idx_users_email        ON users (email);
CREATE INDEX idx_users_external_id  ON users (sso_provider, external_id);
CREATE INDEX idx_user_roles_user    ON user_roles (user_id);
CREATE INDEX idx_role_permissions_role ON role_permissions (role_id);
CREATE INDEX idx_proj_assignments_proj ON project_assignments (project_id);
