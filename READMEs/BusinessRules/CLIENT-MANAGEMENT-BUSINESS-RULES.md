# Client Management: Business Rules & Workflows

**Version:** 1.0  
**Date:** 2025-12-04  
**Status:** ACTIVE

---

## 🎯 Objective
Provide a robust system for managing Client organizations within the portal. This is the root entity for all project and routing logic.

## 📜 Core Business Rules

### 1. Client Code (The Identifier)
*   **Uniqueness**: Must be **Globally Unique**.
    *   *Rationale*: Used in URL routing (e.g., `/portal/ACME/dashboard`).
    *   *Constraint*: `UNIQUE (code)` in database.
    *   *Error*: Returns `409 Conflict` (`ROUTING_CLIENT_CODE_EXISTS`).
*   **Format**: Uppercase, Alphanumeric, Underscores only.
    *   *Regex*: `^[A-Z0-9_]+$`
    *   *Example*: `ACME`, `TECH_CORP`, `7ELEVEN`.
    *   *Invalid*: `Acme Inc`, `client-1`.
*   **Immutability**: Can be changed, but discouraged.
    *   *Impact*: Changing code breaks existing bookmarks if URLs are constructed using codes.
    *   *Decision*: Allowed, but UI should warn user.

### 2. Client Name (The Display)
*   **Uniqueness**: Not enforced.
    *   *Rationale*: "Acme Corp" (US) and "Acme Corp" (UK) might be separate entities in the system.
*   **Format**: Standard text, max 255 chars.

### 3. Status (The Switch)
*   **Values**: `Active` | `Inactive`.
*   **Behavior**:
    *   `Inactive` clients are hidden from navigation menus.
    *   `Inactive` clients block access to their projects/routes.

### 4. Deletion (The Safety)
*   **Soft Delete**: All deletions are soft (`is_deleted = TRUE`).
*   **Hard Constraint**: Cannot delete a client if it has **Projects**.
    *   *Error*: Database `ON DELETE RESTRICT`.
    *   *UI*: Must prompt user to delete/archive projects first.

---

## 🔄 Workflows

### Create Client
1.  **User Action**: Clicks "Add Client".
2.  **Input**: Name, Code, Status.
3.  **Validation**:
    *   Code format check.
    *   Code uniqueness check (API returns 409).
4.  **Outcome**:
    *   Success: Client created, user redirected to Client List.
    *   Failure: Error message displayed.

### Update Client
1.  **User Action**: Clicks "Edit" on Client Card.
2.  **Input**: Name, Code, Status.
3.  **Validation**:
    *   If Code changed: Check uniqueness.
4.  **Outcome**:
    *   Success: Client updated.

---

## 🛡️ API Contract

### Endpoints
*   `POST /api/v1/portal-routing/clients`
*   `PUT /api/v1/portal-routing/clients/{id}`
*   `GET /api/v1/portal-routing/clients/{id}`
*   `GET /api/v1/portal-routing/clients`

### Error Codes
| Code | HTTP Status | Meaning |
|------|-------------|---------|
| `ROUTING_CLIENT_CODE_EXISTS` | 409 | Client Code is already taken. |
| `CLIENT_NOT_FOUND` | 404 | Client ID does not exist. |
| `VALIDATION_ERROR` | 400 | Invalid format (e.g., lowercase code). |
