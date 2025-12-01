
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