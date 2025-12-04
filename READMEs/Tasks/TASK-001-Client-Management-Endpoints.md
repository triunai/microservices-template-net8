# 🚀 Task: Implement Client Management Endpoints

## 📋 Overview
Implement the missing CRUD endpoints for Client Management in the Portal Routing module. This includes Creating, Updating, and Retrieving single clients. This is a prerequisite for the Frontend Client Management feature.

## 🏗️ Architecture
- **Module**: Portal Routing
- **Pattern**: CQRS (Commands/Queries) + Vertical Slice Architecture
- **Database**: PostgreSQL (`clients` table)

## 🗄️ Schema Definition (Source of Truth)
Based on `READMEs/SQL/PostgreSQL/Migrations/03-portal-routing-schema.sql`:
```sql
CREATE TABLE clients (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    code VARCHAR(50) NOT NULL,  -- Globally Unique (Partial Index: WHERE is_deleted = FALSE)
    name VARCHAR(255) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active', 'Inactive')),
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id)
);
```

## 🧠 Context Hydration (Pre-Dev Checklist)
Before starting development, verify the following files and tables to ensure full context:
1.  **Schema Source of Truth**: `READMEs/SQL/PostgreSQL/Migrations/03-portal-routing-schema.sql` (Lines 27-63 for `clients` table).
2.  **Existing Read DAC**: `Rgt.Space.Infrastructure/Persistence/Dac/PortalRouting/ClientReadDac.cs` (Check existing methods to avoid duplication).
3.  **Existing Write DAC**: `Rgt.Space.Infrastructure/Persistence/Dac/PortalRouting/ClientWriteDac.cs` (Check existing methods).
4.  **Read Model**: `Rgt.Space.Core/ReadModels/ClientReadModel.cs` (Ensure it matches the schema).
5.  **Database Table**: Query `clients` table in PostgreSQL to see existing data/constraints.

## 📝 Implementation Plan

### 1. Infrastructure Layer (Persistence)
- [x] **Read DAC (`IClientReadDac`)**
    - **Add Method**: `GetByIdAsync(Guid id, CancellationToken ct)`
    - **SQL**: `SELECT id, name, code, status FROM clients WHERE id = @Id AND is_deleted = FALSE`
    - **Guardrail**: Ensure `is_deleted = FALSE` is always included to respect soft deletes.
- [x] **Write DAC (`IClientWriteDac`)**
    - **Add Method**: `CreateAsync(Guid id, string name, string code, string status, Guid createdBy, CancellationToken ct)`
    - **SQL**: 
      ```sql
      INSERT INTO clients (id, name, code, status, created_by, updated_by) 
      VALUES (@Id, @Name, @Code, @Status, @CreatedBy, @CreatedBy)
      ```
    - **Add Method**: `UpdateAsync(Guid id, string name, string code, string status, Guid updatedBy, CancellationToken ct)`
    - **SQL**:
      ```sql
      UPDATE clients 
      SET name = @Name, code = @Code, status = @Status, updated_by = @UpdatedBy, updated_at = NOW() AT TIME ZONE 'utc'
      WHERE id = @Id AND is_deleted = FALSE
      ```
    - **Guardrail**: Handle `PostgresException` (23505) for unique code violation.

### 2. Application Layer (CQRS)
- [x] **Commands**
    - `CreateClient.Command`:
        - **Validator**: 
            - `Name`: Required, Max 255 chars.
            - `Code`: Required, Max 50 chars, Uppercase, No Spaces (Regex: `^[A-Z0-9_]+$`).
            - `Status`: Must be 'Active' or 'Inactive'.
        - **Handler**: 
            - Check uniqueness via `ReadDac.GetByCodeAsync` (if exists).
            - Call `WriteDac.CreateAsync`.
    - `UpdateClient.Command`:
        - **Validator**: Same as Create + `Id` Required.
        - **Handler**: 
            - Check existence via `ReadDac.GetByIdAsync`.
            - Check uniqueness if code changes.
            - Call `WriteDac.UpdateAsync`.
- [x] **Queries**
    - `GetClientById.Query`:
        - **Handler**: Call `ReadDac.GetByIdAsync`. Return `ClientReadModel` or 404.

### 3. API Layer (Endpoints)
- [x] **Create Endpoint**
    - **URL**: `POST /api/v1/portal-routing/clients`
    - **Request**: `{ name, code, status }`
    - **Response**: `201 Created` with `{ id }`
- [x] **Update Endpoint**
    - **URL**: `PUT /api/v1/portal-routing/clients/{id}`
    - **Request**: `{ name, code, status }`
    - **Response**: `200 OK`
- [x] **GetById Endpoint**
    - **URL**: `GET /api/v1/portal-routing/clients/{id}`
    - **Response**: `200 OK` with Client details.

### 4. Verification
- [ ] **Build**: Ensure solution builds without errors.
- [ ] **Swagger**: Verify endpoints appear and work correctly.
- [ ] **Database**: Verify data is inserted/updated in `clients` table.
