
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