# Implementation Plan: ERP Finalization (The "Last Mile")

**Status:** Planned  
**Date:** 2025-12-01  
**Goal:** Complete the remaining 20% of the ERP system to reach "Production Ready" status, focusing on Security, RBAC, and Dashboards.

---

## 🏗️ Phase 1: User Access Rights (RBAC Matrix)
**Objective:** Implement the granular permission system shown in the "User Access Rights" screenshot.

### 1.1 Permission Read Endpoints
*   **Endpoint:** `GET /api/v1/users/{userId}/permissions`
*   **Logic:** Fetch effective permissions (Base Role + Overrides).
*   **Response:** List of Modules with `CanView`, `CanInsert`, `CanEdit` flags.

### 1.2 Permission Write Endpoints (Overrides)
*   **Endpoint:** `POST /api/v1/users/{userId}/permissions/grant`
*   **Endpoint:** `POST /api/v1/users/{userId}/permissions/revoke`
*   **Logic:** Insert/Update rows in `user_permission_overrides` table.
*   **Audit:** Must log who changed whose permissions.

---

## 🔐 Phase 2: Real Authentication (The "Keys")
**Objective:** Secure the API by enforcing JWT validation and User Context injection.

### 2.1 Enable Auth Middleware
*   **Action:** Remove `AllowAnonymous()` from all endpoints.
*   **Action:** Configure `JwtBearer` middleware to validate tokens from the SSO Broker.

### 2.2 User Context Injection
*   **Action:** Create `ICurrentUser` service to extract `UserId` and `TenantId` from JWT claims.
*   **Refactor:** Update all Commands (e.g., `UpdateUser`, `AssignUser`) to use `_currentUser.Id` instead of hardcoded `Guid.Empty` or request params.

---

## 📊 Phase 3: Dashboard Aggregates
**Objective:** Power the "Dashboard" screen with instant statistics.

### 3.1 Dashboard Stats Endpoint
*   **Endpoint:** `GET /api/v1/dashboard/stats`
*   **Logic:**
    *   Count `Active` assignments.
    *   Count `Pending` (Vacant mandatory positions).
    *   Count `Completed` projects.
    *   Calculate "Assignment Distribution" pie chart data.
*   **Performance:** Use optimized SQL `COUNT` queries, do not load all records into memory.

---

## ✅ Checklist
- [x] **Phase 1:** Implement `Get/Grant/Revoke` Permission endpoints. (Completed 2025-12-02)
- [x] **Phase 2:** Enable JWT Auth & Refactor `CurrentUserId`. (Completed 2025-12-02)
- [ ] **Phase 3:** Implement `GetDashboardStats` endpoint.
