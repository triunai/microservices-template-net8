# Project Management: Business Rules & Workflows

**Version:** 1.0  
**Date:** 2025-12-04  
**Status:** DRAFT

---

## 🎯 Objective
Manage Projects (Applications) under a specific Client. A Project represents a distinct application or system (e.g., "POS", "Inventory", "HR Portal").

## 📜 Core Business Rules

### 1. Project Code (The Identifier)
*   **Scope**: Unique **PER CLIENT**.
    *   *Constraint*: `UNIQUE (client_id, code)` in database.
    *   *Rationale*: Client A can have "POS", Client B can have "POS".
    *   *Error*: Returns `409 Conflict` (`PROJECT_CODE_EXISTS_IN_CLIENT`).
*   **Format**: Uppercase, Alphanumeric, Underscores only.
    *   *Regex*: `^[A-Z0-9_]+$`

### 2. Ownership (The Parent)
*   **Mandatory**: Every project **MUST** belong to a Client.
*   **Immutable**: Moving a project between clients is NOT supported in V1 (requires complex data migration).

### 3. External URL (The Destination)
*   **Optional**: Can be NULL.
*   **Purpose**: The actual URL of the application (e.g., `https://pos.acme.com`).
*   **Distinction**: This is *different* from the "Routing URL" (which is the portal path, e.g., `/portal/acme/pos`).

### 4. Status
*   **Values**: `Active` | `Inactive`.

---

## 🔄 Workflows

### Create Project
1.  **User Action**: Clicks "Add Project" (usually from Client Detail view).
2.  **Context**: `ClientId` is known.
3.  **Input**: Name, Code, Status, External URL.
4.  **Validation**:
    *   Code uniqueness check (within this Client).
5.  **Outcome**:
    *   Success: Project created.

### Update Project
1.  **User Action**: Clicks "Edit" on Project Card.
2.  **Input**: Name, Code, Status, External URL.
3.  **Validation**:
    *   If Code changed: Check uniqueness within same Client.
4.  **Outcome**:
    *   Success: Project updated.

---

## 🛡️ API Contract

### Endpoints
*   `POST /api/v1/portal-routing/projects`
*   `PUT /api/v1/portal-routing/projects/{id}`
*   `GET /api/v1/portal-routing/projects/{id}`

### Error Codes
| Code | HTTP Status | Meaning |
|------|-------------|---------|
| `PROJECT_CODE_EXISTS_IN_CLIENT` | 409 | Project Code already exists for this Client. |
| `PROJECT_NOT_FOUND` | 404 | Project ID does not exist. |
| `CLIENT_NOT_FOUND` | 404 | Client ID (for creation) does not exist. |

---

## 🧭 Code Reference Context (For AI Context Hydration)
*Use these references to quickly understand the existing patterns and architecture.*

### 1. Database Schema
*   **Source**: `READMEs/SQL/PostgreSQL/Tables/projects.sql`
*   **Key Constraints**: `idx_projects_client_code_active` (Unique Index), `client_id` FK.

### 2. Infrastructure (DACs)
*   **Reference Implementation**: `Rgt.Space.Infrastructure/Persistence/Dac/PortalRouting/ClientWriteDac.cs` (Copy this pattern).
*   **Target Interface**: `Rgt.Space.Core/Abstractions/PortalRouting/IProjectWriteDac.cs` (To be created).
*   **Target Implementation**: `Rgt.Space.Infrastructure/Persistence/Dac/PortalRouting/ProjectWriteDac.cs` (To be created).

### 3. Application (CQRS)
*   **Reference Command**: `Rgt.Space.Infrastructure/Commands/PortalRouting/CreateClient.cs` (Copy this pattern).
*   **Target Commands**: 
    *   `Rgt.Space.Infrastructure/Commands/PortalRouting/CreateProject.cs`
    *   `Rgt.Space.Infrastructure/Commands/PortalRouting/UpdateProject.cs`

### 4. API (Endpoints)
*   **Reference Endpoint**: `Rgt.Space.API/Endpoints/PortalRouting/CreateClient/Endpoint.cs` (Copy this pattern).
*   **Target Endpoints**:
    *   `Rgt.Space.API/Endpoints/PortalRouting/CreateProject/Endpoint.cs`
    *   `Rgt.Space.API/Endpoints/PortalRouting/UpdateProject/Endpoint.cs`

### 5. Error Handling
*   **Catalog**: `Rgt.Space.Core/Errors/ErrorCatalog.cs` (Add new error codes here).
