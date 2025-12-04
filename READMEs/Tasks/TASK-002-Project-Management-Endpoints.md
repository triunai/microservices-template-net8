# Task 002: Project Management Endpoints

## 🎯 Objective
Implement the missing **Project Management Endpoints** (`Create`, `Update`) in the `Rgt.Space.API`. This will allow the frontend to create and manage projects under specific clients.

## 📜 Business Rules (from `PORTAL-ROUTING-BUSINESS-RULES.md`)
1.  **Ownership**: Every project **MUST** belong to a client (`client_id` NOT NULL).
2.  **Uniqueness**: Project Code must be unique **WITHIN** a client (Composite Unique Index: `client_id` + `code`).
    *   *Example*: Client A can have "POS", Client B can have "POS". Client A cannot have two "POS" projects.
3.  **Soft Deletes**: Uniqueness check must respect `is_deleted = FALSE`.
4.  **Status**: Must be 'Active' or 'Inactive'.
5.  **Audit**: Must populate `created_by`, `updated_by`.

## 🛠️ Implementation Plan

### 1. Infrastructure Layer (Persistence)
- [x] **Create Write DAC (`IProjectWriteDac`)**
    - **Interface**: `IProjectWriteDac`
    - **Implementation**: `ProjectWriteDac`
    - **Methods**:
        - `CreateAsync(Guid id, Guid clientId, string name, string code, string status, string? externalUrl, Guid createdBy, CancellationToken ct)`
        - `UpdateAsync(Guid id, string name, string code, string status, string? externalUrl, Guid updatedBy, CancellationToken ct)`
    - **SQL**:
        - `INSERT INTO projects ...`
        - `UPDATE projects ... WHERE id = @Id AND is_deleted = FALSE`
    - **Error Handling**: Catch `PostgresException` (23505) -> Throw `ConflictException` with code `PROJECT_CODE_EXISTS_IN_CLIENT`.

### 2. Application Layer (CQRS)
- [x] **Create Project Command** (`CreateProject.cs`)
    - **Command**: `Guid ClientId`, `string Name`, `string Code`, `string Status`, `string? ExternalUrl`
    - **Validator**:
        - `ClientId`: Required, Not Empty.
        - `Name`: Required, Max 255.
        - `Code`: Required, Max 50, Regex `^[A-Z0-9_]+$`.
        - `Status`: 'Active' or 'Inactive'.
    - **Handler**:
        - Check if Client exists (`IClientReadDac.GetByIdAsync`). Return `CLIENT_NOT_FOUND` if null.
        - Check uniqueness (`IProjectReadDac.GetByClientAndCodeAsync` - *Need to add this to ReadDac*).
        - Generate `Uuid7`.
        - Call `WriteDac.CreateAsync`.

- [x] **Update Project Command** (`UpdateProject.cs`)
    - **Command**: `Guid Id`, `string Name`, `string Code`, `string Status`, `string? ExternalUrl`
    - **Validator**: Similar to Create.
    - **Handler**:
        - Check if Project exists. Return `PROJECT_NOT_FOUND`.
        - Check uniqueness if Code changed (within the same Client).
        - Call `WriteDac.UpdateAsync`.

- [x] **Update Read DAC** (`IProjectReadDac`)
    - Add `GetByClientAndCodeAsync(Guid clientId, string code, CancellationToken ct)` to support uniqueness checks.

### 3. API Layer (Endpoints)
- [x] **Create Endpoint**
    - **URL**: `POST /api/v1/portal-routing/projects`
    - **Request**: `{ clientId, name, code, status, externalUrl }`
    - **Response**: `201 Created` with `{ id }`
    - **Tags**: "Project Management"

- [x] **Update Endpoint**
    - **URL**: `PUT /api/v1/portal-routing/projects/{id}`
    - **Request**: `{ name, code, status, externalUrl }`
    - **Response**: `200 OK`
    - **Tags**: "Project Management"

- [x] **Get By ID Endpoint** (Added for completeness)
    - **URL**: `GET /api/v1/portal-routing/projects/{id}`

- [x] **Get All Projects Endpoint** (Added for completeness)
    - **URL**: `GET /api/v1/portal-routing/projects`
    - **Response**: `200 OK` with list of projects

- [x] **Delete Endpoint** (Added for completeness)
    - **URL**: `DELETE /api/v1/portal-routing/projects/{id}`
    - **Response**: `204 No Content`
    - **Tags**: "Project Management"

## 🧪 Verification
1.  **Build**: Ensure solution builds.
2.  **Swagger**: Verify new endpoints appear under "Project Management".
3.  **Test Scenarios**:
    - Create Project (Success).
    - Create Duplicate Code for SAME Client (Fail - 409).
    - Create Duplicate Code for DIFFERENT Client (Success - 201).
    - Create Project for Non-Existent Client (Fail - 404).
    - Delete Project (Success - 204).
    - Delete Client with Projects (Fail - 409).
