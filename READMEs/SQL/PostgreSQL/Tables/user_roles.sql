
CREATE TABLE user_roles (
    id                  UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id             UUID        NOT NULL REFERENCES users   (id),
    role_id             UUID        NOT NULL REFERENCES roles   (id),
    assigned_by_user_id UUID        NULL REFERENCES users (id),
    assigned_at         TIMESTAMP   NOT NULL DEFAULT now(),

    CONSTRAINT user_roles_uk UNIQUE (user_id, role_id)
);