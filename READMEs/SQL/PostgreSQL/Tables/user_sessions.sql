
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