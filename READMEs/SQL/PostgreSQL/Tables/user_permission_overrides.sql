
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
