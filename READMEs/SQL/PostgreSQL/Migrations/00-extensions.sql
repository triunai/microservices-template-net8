-- ==========================================
-- 0. EXTENSIONS & UUID CONFIGURATION
-- ==========================================
-- Target Database: All Databases

-- 1. Enable pgcrypto (Required for gen_random_bytes if using polyfill, or general utility)
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- 2. UUID v7 Setup
-- Logic: Check if native uuidv7() exists (PostgreSQL 18+). 
-- If NOT, create the polyfill function.

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
          uuid_bytes = set_byte(uuid_bytes, 6, (get_byte(uuid_bytes, 6) & x'0f'::int) | x'70'::int);
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
