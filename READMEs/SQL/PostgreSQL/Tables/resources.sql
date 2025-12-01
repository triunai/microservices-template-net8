
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