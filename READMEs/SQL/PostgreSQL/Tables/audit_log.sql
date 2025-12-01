
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
