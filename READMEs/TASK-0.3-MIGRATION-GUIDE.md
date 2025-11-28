# Task 0.3: PostgreSQL Schema Migration Guide

## 📍 **Current Status (as of 2025-11-27)**

### ✅ **Completed Tasks:**
- **Task 0.1: Project Renaming** - `MicroservicesBase` → `Rgt.Space` ✅
  - Solution file, project files, namespaces, and all references updated
  - Build successful with 0 errors, 0 warnings
  
- **Task 0.2: PostgreSQL Migration (Infrastructure)** - ✅
  - NuGet packages migrated: `Microsoft.Data.SqlClient` → `Npgsql`
  - All C# code updated to use `NpgsqlConnection`
  - Connection strings configured in `appsettings.json`:
    ```json
    "TenantMaster": "Host=localhost;Database=tenant_master;Username=postgres;Password=123456;",
    "RgtAuthPrototype": "Host=localhost;Database=rgt_auth_prototype;Username=postgres;Password=123456;",
    "RgtAuthAudit": "Host=localhost;Database=rgt_auth_audit;Username=postgres;Password=123456;"
    ```
  - Queries updated to PostgreSQL syntax (snake_case, no `dbo.` schema prefix)
  - Error handling updated for PostgreSQL `SqlState` codes
  - Build successful ✅

### 🎯 **Next Task: 0.3 - Implement UAM Schema in PostgreSQL**

---

## 📋 **Task 0.3 Objectives**

Migrate existing MSSQL database schemas to PostgreSQL, including:

1. **Tenant Master Database** (`tenant_master`)
   - `tenants` table (for multi-tenancy routing)
   - Reference data tables (if any)

2. **UAM Database** (`rgt_auth_prototype`)
   - User Access Management tables (users, roles, permissions, etc.)
   - As defined in `READMEs/SQL/UAM-schema(latest).md`

3. **Audit Database** (`rgt_auth_audit`)
   - `audit_log` table (for API request/response auditing)
   - SSO audit tables (if shared with vBroker API)

4. **Database Objects Migration**
   - Stored Procedures → PostgreSQL Functions
   - User-Defined Types (UDT) → PostgreSQL Composite Types
   - Table-Valued Parameters (TVP) → PostgreSQL Arrays/JSONB
   - Scalar Functions → PostgreSQL Functions
   - Triggers → PostgreSQL Triggers

---

## 🗂️ **What You Need to Provide**

Since you already have these schemas in MSSQL, please provide the following:

### **1. CREATE TABLE Scripts**
Extract `CREATE TABLE` scripts from your MSSQL databases:

```sql
-- From MSSQL, run:
-- For each database, generate CREATE scripts

-- Example for tenant_master:
SELECT 
    'CREATE TABLE ' + SCHEMA_NAME(schema_id) + '.' + name + ' (...);' 
FROM sys.tables 
WHERE type = 'U'
ORDER BY name;
```

**Databases to extract:**
- `TenantMaster` (MSSQL) → `tenant_master` (PostgreSQL)
- `RgtAuthPrototype` (MSSQL) → `rgt_auth_prototype` (PostgreSQL)
- `RgtAuthAudit` (MSSQL) → `rgt_auth_audit` (PostgreSQL)

**Specific tables needed:**
- `tenants` (tenant routing table)
- `audit_log` (API audit table - the one used by THIS API, not SSO)
- All UAM tables (users, roles, permissions, etc.)
- SSO audit tables (if shared with vBroker API)

### **2. Stored Procedures**
```sql
-- Extract stored procedure definitions
SELECT 
    OBJECT_NAME(object_id) AS ProcedureName,
    OBJECT_DEFINITION(object_id) AS Definition
FROM sys.procedures
WHERE type = 'P'
ORDER BY name;
```

**Key procedures to migrate:**
- `GetSaleWithItems` (currently referenced in `SalesReadDac.cs`)
- Any UAM-related procedures
- Any audit-related procedures

### **3. User-Defined Types (UDT)**
```sql
-- Extract UDT definitions
SELECT 
    t.name AS TypeName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length,
    c.precision,
    c.scale
FROM sys.table_types t
INNER JOIN sys.columns c ON t.type_table_object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
ORDER BY t.name, c.column_id;
```

### **4. Table-Valued Parameters (TVP)**
List any TVPs used in stored procedures (these will become PostgreSQL arrays or JSONB)

### **5. Scalar Functions**
```sql
-- Extract scalar function definitions
SELECT 
    OBJECT_NAME(object_id) AS FunctionName,
    OBJECT_DEFINITION(object_id) AS Definition
FROM sys.objects
WHERE type IN ('FN', 'IF', 'TF')
ORDER BY name;
```

### **6. Triggers**
```sql
-- Extract trigger definitions
SELECT 
    t.name AS TriggerName,
    OBJECT_NAME(t.parent_id) AS TableName,
    OBJECT_DEFINITION(t.object_id) AS Definition
FROM sys.triggers t
WHERE is_ms_shipped = 0
ORDER BY t.name;
```

### **7. Indexes**
```sql
-- Extract index definitions
SELECT 
    i.name AS IndexName,
    OBJECT_NAME(i.object_id) AS TableName,
    i.type_desc,
    i.is_unique,
    COL_NAME(ic.object_id, ic.column_id) AS ColumnName
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE i.type > 0 -- Exclude heaps
  AND i.is_primary_key = 0 -- Exclude PKs (handled in CREATE TABLE)
  AND i.is_unique_constraint = 0 -- Exclude UQs (handled in CREATE TABLE)
ORDER BY i.name, ic.key_ordinal;
```

---

## 🔄 **Migration Strategy**

### **Phase 1: Schema Conversion**
1. Convert MSSQL `CREATE TABLE` scripts to PostgreSQL syntax:
   - `NVARCHAR(MAX)` → `TEXT`
   - `NVARCHAR(n)` → `VARCHAR(n)`
   - `DATETIME2` → `TIMESTAMP`
   - `BIT` → `BOOLEAN`
   - `UNIQUEIDENTIFIER` → `UUID`
   - `VARBINARY(MAX)` → `BYTEA`
   - `[dbo].[TableName]` → `table_name` (snake_case, no schema prefix for `public`)
   - `IDENTITY(1,1)` → `GENERATED ALWAYS AS IDENTITY` or `SERIAL`
   - `DEFAULT NEWID()` → `DEFAULT uuid_generate_v7()` (for time-ordered UUIDs)

2. Convert constraints:
   - `PRIMARY KEY CLUSTERED` → `PRIMARY KEY`
   - `FOREIGN KEY` syntax is similar, just adjust table/column names
   - `CHECK` constraints are similar
   - `DEFAULT` constraints are similar

### **Phase 2: Stored Procedures → Functions**
1. Convert T-SQL to PL/pgSQL:
   - `CREATE PROCEDURE` → `CREATE OR REPLACE FUNCTION`
   - `@parameter` → `parameter` (no `@` prefix)
   - `DECLARE @var` → `DECLARE var`
   - `SET @var = value` → `var := value`
   - `SELECT ... INTO @var` → `SELECT ... INTO var`
   - `RETURN` → `RETURN` (for scalar functions) or `RETURN QUERY` (for table functions)
   - `OUTPUT` parameters → Use `OUT` or `INOUT` parameters

2. Handle result sets:
   - Single result set: `RETURNS TABLE(...)` or `RETURNS SETOF record_type`
   - Multiple result sets: Use `refcursor` (complex, may need refactoring)

### **Phase 3: UDT/TVP → PostgreSQL Equivalents**
1. **UDT (User-Defined Types)**:
   - Convert to `CREATE TYPE ... AS (...)` (composite type)
   - Or use `CREATE DOMAIN` for simple types with constraints

2. **TVP (Table-Valued Parameters)**:
   - Option 1: Use `JSONB` parameter (most flexible)
   - Option 2: Use PostgreSQL arrays `parameter_name type_name[]`
   - Option 3: Use temporary tables

### **Phase 4: Triggers**
1. Convert T-SQL triggers to PL/pgSQL:
   - `CREATE TRIGGER` syntax is similar
   - `AFTER INSERT, UPDATE, DELETE` → Same
   - `INSTEAD OF` → Same
   - `inserted` table → `NEW` record (for row-level triggers)
   - `deleted` table → `OLD` record (for row-level triggers)
   - Must return `NEW` or `OLD` or `NULL`

### **Phase 5: Indexes**
1. Convert index definitions:
   - `CREATE INDEX` syntax is similar
   - `INCLUDE` columns → PostgreSQL doesn't support, use covering index pattern
   - `CLUSTERED` → PostgreSQL uses `CLUSTER` command separately
   - `NONCLUSTERED` → Default in PostgreSQL

---

## 📝 **Expected Deliverables for Task 0.3**

Create the following SQL script files in `READMEs/SQL/PostgreSQL/`:

1. **`01-tenant-master-schema.sql`**
   - `tenants` table definition
   - Indexes
   - Seed data (initial tenants)

2. **`02-uam-schema.sql`**
   - All UAM tables (users, roles, permissions, etc.)
   - Foreign keys
   - Indexes
   - Reference data seeding

3. **`03-audit-schema.sql`**
   - `audit_log` table (for THIS API)
   - SSO audit tables (if applicable)
   - Indexes
   - Partitioning strategy (optional, for performance)

4. **`04-functions.sql`**
   - `uuid_generate_v7()` (already defined in `UAM-schema(latest).md`)
   - Migrated stored procedures as functions
   - Scalar functions

5. **`05-triggers.sql`**
   - Migrated triggers

6. **`06-seed-data.sql`**
   - Initial reference data
   - Test tenants
   - Test users/roles (if applicable)

---

## 🔍 **Key Considerations**

### **1. Tenant Table Schema**
The `tenants` table is critical for multi-tenancy routing. Current code expects:

```sql
-- PostgreSQL version (based on code analysis)
CREATE TABLE tenants (
    id                UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    code              VARCHAR(50) NOT NULL UNIQUE,  -- Tenant identifier (e.g., '7ELEVEN')
    name              VARCHAR(255) NOT NULL,        -- Display name
    connection_string TEXT        NOT NULL,         -- Connection string to tenant's database
    status            VARCHAR(20) NOT NULL DEFAULT 'Active', -- 'Active', 'Inactive', 'Suspended'
    created_at        TIMESTAMP   NOT NULL DEFAULT now(),
    updated_at        TIMESTAMP   NOT NULL DEFAULT now()
);

CREATE INDEX idx_tenants_status ON tenants(status) WHERE status = 'Active';
```

**Code Dependencies:**
- `MasterTenantConnectionFactory.cs` queries: `SELECT connection_string FROM tenants WHERE code = @tenant_code AND status = 'Active'`
- `CacheWarmupHostedService.cs` queries: `SELECT code FROM tenants WHERE status = 'Active'`

### **2. Audit Log Table Schema**
The `audit_log` table is used by `AuditLogger.cs`. Current code expects:

```sql
-- PostgreSQL version (based on code analysis)
CREATE TABLE audit_log (
    id                UUID        PRIMARY KEY DEFAULT uuid_generate_v7(),
    tenant_id         VARCHAR(50) NOT NULL,  -- Tenant code (string, not UUID)
    user_id           VARCHAR(255),          -- User identifier from JWT
    client_id         VARCHAR(255),          -- Client/app identifier
    ip_address        VARCHAR(45),           -- IPv4/IPv6
    user_agent        TEXT,
    
    action            VARCHAR(100) NOT NULL, -- e.g., 'Sales.Read', 'Sales.Create'
    entity_type       VARCHAR(100),          -- e.g., 'Sale', 'Customer'
    entity_id         VARCHAR(100),          -- Entity identifier
    
    timestamp         TIMESTAMP   NOT NULL DEFAULT now(),
    correlation_id    VARCHAR(100),
    request_path      VARCHAR(500),
    
    is_success        BOOLEAN     NOT NULL,
    status_code       INTEGER,
    error_code        VARCHAR(50),
    error_message     TEXT,
    duration_ms       INTEGER,
    
    request_data      BYTEA,                 -- Gzipped JSON
    response_data     BYTEA,                 -- Gzipped JSON
    delta             BYTEA,                 -- Gzipped JSON (before/after)
    
    idempotency_key   VARCHAR(100),
    source            VARCHAR(50) NOT NULL DEFAULT 'API',
    request_hash      VARCHAR(64)            -- SHA256
);

CREATE INDEX idx_audit_tenant_timestamp ON audit_log(tenant_id, timestamp DESC);
CREATE INDEX idx_audit_correlation ON audit_log(correlation_id) WHERE correlation_id IS NOT NULL;
CREATE INDEX idx_audit_user ON audit_log(user_id, timestamp DESC) WHERE user_id IS NOT NULL;
```

**Code Dependencies:**
- `AuditLogger.cs` inserts into this table with the exact column names shown above

### **3. UUID v7 Function**
Already defined in `READMEs/SQL/UAM-schema(latest).md`. Must be created in all databases:

```sql
CREATE OR REPLACE FUNCTION uuid_generate_v7()
RETURNS uuid
AS $$
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
$$ LANGUAGE plpgsql;
```

### **4. Sales Module (Template Code)**
The `SalesReadDac.cs` references a stored procedure `get_sale_with_items`. This is template code and may not exist in your MSSQL database. You can either:
- **Option A:** Create a dummy function for now (won't be used until Sales module is implemented)
- **Option B:** Skip it (will cause runtime errors if Sales endpoints are called)

---

## 🚀 **Action Plan for Fresh Chat**

### **Step 1: Gather MSSQL Scripts**
Run the SQL queries above to extract:
- All `CREATE TABLE` scripts
- All stored procedures
- All UDTs, TVPs, functions, triggers
- All indexes

### **Step 2: Provide Context**
In the new chat, provide:
1. This migration guide (`TASK-0.3-MIGRATION-GUIDE.md`)
2. The extracted MSSQL scripts
3. Specify which tables are for:
   - Tenant routing (`tenants`)
   - UAM (user/role/permission management)
   - Audit logging (API audit vs SSO audit)

### **Step 3: Request Conversion**
Ask the AI to:
1. Convert MSSQL scripts to PostgreSQL
2. Create the 6 SQL files listed in "Expected Deliverables"
3. Ensure compatibility with the C# code (column names, data types)
4. Add appropriate indexes for performance
5. Include seed data for testing

### **Step 4: Validation**
After migration:
1. Run the SQL scripts in PostgreSQL
2. Test the application startup (health checks, cache warmup)
3. Verify tenant routing works
4. Verify audit logging works

---

## 📚 **Reference Files**

- **UAM Schema Reference:** `READMEs/SQL/UAM-schema(latest).md`
- **Implementation Roadmap:** `READMEs/IMPLEMENTATION-ROADMAP.md`
- **Connection Strings:** `Rgt.Space.API/appsettings.json`
- **Code Dependencies:**
  - `Rgt.Space.Infrastructure/Tenancy/MasterTenantConnectionFactory.cs`
  - `Rgt.Space.Infrastructure/Tenancy/CacheWarmupHostedService.cs`
  - `Rgt.Space.Infrastructure/Auditing/AuditLogger.cs`
  - `Rgt.Space.Infrastructure/Persistence/SalesReadDac.cs`
  - `Rgt.Space.Core/Constants/SqlConstants.cs`
  - `Rgt.Space.Core/Constants/StoredProcedureNames.cs`

---

## ✅ **Success Criteria**

Task 0.3 is complete when:
1. All PostgreSQL databases are created (`tenant_master`, `rgt_auth_prototype`, `rgt_auth_audit`)
2. All tables are created with correct schema
3. UUID v7 function is installed in all databases
4. Seed data is loaded (at least one test tenant)
5. Application starts successfully:
   - Health checks pass (`/health/ready` returns 200)
   - Cache warmup completes without errors
   - Tenant routing works (can resolve tenant connection strings)
6. Audit logging works (can write audit entries to `audit_log` table)

---

## 🎯 **Next Steps After 0.3**

Once Task 0.3 is complete, proceed to:
- **Task 1.1:** Implement UAM CQRS endpoints (User management)
- **Task 1.2:** Implement Role management
- **Task 1.3:** Implement Permission management
- **Task 1.4:** Implement Client management

---

**Good luck with the migration! 🚀**
