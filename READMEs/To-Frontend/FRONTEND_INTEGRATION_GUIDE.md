# 🤝 Frontend Integration Guide

**Version:** 1.0 (Backend Phase 3 Complete)
**Date:** 2025-12-02

This document serves as the **Single Source of Truth** for the Frontend AI/Developer to integrate with the `ReactPortal` backend.

---

## 1. Authentication & SSO (The Handshake)

### Configuration
*   **Authority (SSO Broker):** `https://localhost:7012`
*   **Audience (API):** `rgt-space-portal-api`
*   **Client ID:** `react-portal-client` (Ensure this matches SSO config)
*   **Scopes:** `openid profile email offline_access`

### The Flow
1.  **Login (Enforced Context):**
    *   **Goal:** This application is the **RGTSPACE ERP**. It must always log in to the `RGT_SPACE_PORTAL` tenant.
    *   **Action:** Always pass `acr_values=tenant:RGT_SPACE_PORTAL` (or `tenant=RGT_SPACE_PORTAL`) when redirecting to the SSO Broker.
    *   **Result:** The user logs in as a System Admin/Employee of RGT Space, granting access to the "God View" dashboard.
    *   **Note:** Do not implement tenant switching UI for this phase.
2.  **Callback:** Receive `access_token` (JWT) and `refresh_token`.
3.  **Storage:** Store tokens securely.
4.  **Requests:** Attach `Authorization: Bearer <access_token>` to **ALL** requests to the Portal API.

### Token Handling
*   **Expiration:** Access tokens are short-lived (e.g., 15 mins).
*   **Refresh:** On `401 Unauthorized`, use the `refresh_token` against the SSO Broker.
*   **X-Tenant Header (Critical):**
    *   **Rule:** For ALL requests to the **SSO Broker API** (e.g., Refresh, Logout), you **MUST** include the `X-Tenant` header matching the current context.
    *   **Example:** `X-Tenant: RGT_SPACE_PORTAL`
    *   **Note:** The Portal API (ERP) reads the `tid` from the JWT, but the SSO Broker relies on this header.

---

## 2. API Endpoints (Portal API)

**Base URL:** `https://localhost:60304`

### ❓ Frontend Q&A (Clarifications)
*   **X-Tenant Header:**
    *   **Portal API (60304):** Does **NOT** require `X-Tenant`. It uses the `tid` claim in the JWT.
    *   **SSO Broker (7012):** **REQUIRES** `X-Tenant` for Refresh/Logout endpoints.
*   **RBAC Scope:**
    *   Permissions returned are **Global** to the system (Roles/Permissions are shared across the monolith).

### 📊 Dashboard
*   **GET** `/api/v1/dashboard/stats`
    *   **Purpose:** Populates the main dashboard.
    *   **Response:**
        ```json
        {
          "kpis": {
            "activeAssignments": 6,
            "pendingVacancies": 13,
            "activeProjects": 6,
            "inactiveProjects": 0
          },
          "assignmentDistribution": [
            { "positionCode": "TECH_PIC", "count": 2 },
            ...
          ],
          "topVacancies": [
            { "projectName": "Acme Inventory", "missingPosition": "TECH_PIC" },
            ...
          ]
        }
        ```

### 🛡️ RBAC (Permissions)
*   **GET** `/api/v1/users/{userId}/permissions`
    *   **Purpose:** Fetch effective permissions for UI hiding.
    *   **Logic:** Do **NOT** check Roles. Check Permissions.
    *   **Response:**
        ```json
        [
          {
            "module": "PORTAL_ROUTING",
            "subModule": "CLIENT_NAV",
            "canView": true,
            "canInsert": false,
            "canEdit": false,
            "canDelete": false
          }
        ]
        ```

### 🌐 Portal Routing (Clients & Projects)
*   **GET** `/api/v1/portal-routing/clients`
    *   **Purpose:** List all clients (for navigation/filtering).
*   **GET** `/api/v1/portal-routing/clients/{clientId}/projects`
    *   **Purpose:** List projects for a specific client.
*   **GET** `/api/v1/portal-routing/mappings`
    *   **Purpose:** List all routing mappings (God View list).

### 🧩 Task Allocation
*   **GET** `/api/v1/task-allocation/projects/{projectId}`
    *   **Purpose:** Get assignments for a specific project.
    *   **UI Logic:** The API returns a list of *existing* assignments. The Frontend **MUST** map these to the 6 fixed slots (Tech PIC, etc.) and render "Vacant" for any missing slots.
*   **POST** `/api/v1/task-allocation/assign`
    *   **Purpose:** Assign a user to a position.
    *   **Payload:** `{ "projectId": "...", "userId": "...", "positionCode": "TECH_PIC" }`
*   **DELETE** `/api/v1/projects/{projectId}/assignments/{userId}/{positionCode}`
    *   **Purpose:** Remove a user (Unassign).
    *   **Note:** This uses route parameters, not a JSON body.

### 👤 Identity (Users)
*   **GET** `/api/v1/users`
    *   **Purpose:** Get list of users (for dropdowns).
    *   **Params:** `?searchTerm=...` (Optional)
    *   **Note:** The API automatically filters out "Zombie" (inactive) users by default.

---

## 3. Critical Business Rules (Frontend Must Enforce)

### 3.1 Constants & Enums
Do **NOT** use magic strings. Use these constants:

**Position Codes:**
*   `TECH_PIC`
*   `TECH_BACKUP`
*   `FUNC_PIC`
*   `FUNC_BACKUP`
*   `SUPPORT_PIC`
*   `SUPPORT_BACKUP`

**Status:**
*   `Active`
*   `Inactive`

### 3.2 "Zombie" Protocol
*   The API filters out inactive users (`is_active = false`) from assignment lists.
*   **UI Rule:** If a user is missing from the dropdown, they are likely inactive. Do not try to force-assign them.

### 3.3 "God View" RBAC
*   **UI Rule:** Wrap sensitive buttons (Delete, Edit) in a `<Protect permission="...">` component.
*   **Example:** `<Protect permission="PROJECTS_EDIT"><Button>Edit Project</Button></Protect>`

---

## 4. TypeScript Interfaces (Copy-Paste Ready)

```typescript
// Dashboard
export interface DashboardStats {
  kpis: {
    activeAssignments: number;
    pendingVacancies: number;
    activeProjects: number;
    inactiveProjects: number;
  };
  assignmentDistribution: Array<{
    positionCode: string;
    count: number;
  }>;
  topVacancies: Array<{
    projectName: string;
    missingPosition: string;
  }>;
}

// Permissions
export interface UserPermission {
  module: string;
  subModule: string;
  canView: boolean;
  canInsert: boolean;
  canEdit: boolean;
  canDelete: boolean;
}

// Task Allocation
export interface ProjectAssignment {
  projectId: string;
  projectName: string;
  projectCode: string;
  clientId: string;
  clientName: string;
  userId: string;
  userName: string;
  positionCode: string;
}
```
