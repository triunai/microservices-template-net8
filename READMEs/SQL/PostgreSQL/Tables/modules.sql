
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