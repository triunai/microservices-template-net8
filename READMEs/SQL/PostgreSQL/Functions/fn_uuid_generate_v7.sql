-- =====================================================
-- FUNCTION: uuid_generate_v7()
-- Purpose: Generate UUIDv7 (time-ordered) primary keys
-- Created: Migration 00 (00-extensions.sql)
-- Used by: All tables with UUID PRIMARY KEY DEFAULT uuid_generate_v7()
-- Notes: Auto-detects PostgreSQL 18+ native uuidv7() and wraps it,
--        or creates polyfill using pgcrypto for older versions.
-- Dependencies: pgcrypto extension (for gen_random_bytes in polyfill path)
-- =====================================================

-- Extension required for polyfill path
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Conditional creation: PG 18+ wrapper vs polyfill
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'uuidv7') THEN
        -- Create Polyfill for PG < 18
        CREATE OR REPLACE FUNCTION uuid_generate_v7()
        RETURNS uuid
        AS $func$
        DECLARE
          unix_ts_ms bytea;
          uuid_bytes bytea;
        BEGIN
          unix_ts_ms = substring(int8send(floor(extract(epoch from clock_timestamp()) * 1000)::bigint) from 3);
          uuid_bytes = unix_ts_ms || gen_random_bytes(10);
          -- Set version bits (0111 = v7)
          uuid_bytes = set_byte(uuid_bytes, 6, (get_byte(uuid_bytes, 6) & x'0f'::int) | x'70'::int);
          -- Set variant bits (10xx = RFC 4122)
          uuid_bytes = set_byte(uuid_bytes, 8, (get_byte(uuid_bytes, 8) & x'3f'::int) | x'80'::int);
          RETURN encode(uuid_bytes, 'hex')::uuid;
        END;
        $func$ LANGUAGE plpgsql;

        RAISE NOTICE 'Created uuid_generate_v7() polyfill function.';
    ELSE
        -- Create Wrapper for PG 18+ to maintain consistent function name
        CREATE OR REPLACE FUNCTION uuid_generate_v7()
        RETURNS uuid
        AS $func$
        BEGIN
            RETURN uuidv7();
        END;
        $func$ LANGUAGE plpgsql;

        RAISE NOTICE 'Using native uuidv7() function.';
    END IF;
END
$$;
