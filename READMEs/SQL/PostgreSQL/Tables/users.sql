
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