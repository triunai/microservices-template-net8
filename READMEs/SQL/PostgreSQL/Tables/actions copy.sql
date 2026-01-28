
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
