# Implementation Plan: Task Allocation Module

**Status:** Planned  
**Date:** 2025-12-01  
**Goal:** Implement the "God View" staffing matrix (Read/Write) without schema changes.

---

## 🏗️ Phase 1: Data Access Layer (DAC)

### 1.1 Create `TaskAllocationDac`
*   **Location:** `Rgt.Space.Infrastructure/Persistence/Dacs/TaskAllocationDac.cs`
*   **Interface:** `ITaskAllocationDac`
*   **Dependencies:** `IDbConnectionFactory` (Npgsql)

### 1.2 Implement Read Methods
*   `GetProjectAssignments(Guid projectId)`
    *   **Query:** Join `project_assignments`, `users`, `position_types`.
    *   **Filter:** `WHERE is_deleted = FALSE` AND `users.status = 'Active'` (Zombie Fix).
    *   **Return:** List of DTOs (User details + Position code).

*   `GetMatrixView(Guid? clientId)`
    *   **Query:** Bulk fetch for "God View" dashboard.
    *   **Grouping:** Group by Project -> List of Assignments.

### 1.3 Implement Write Methods
*   `AssignUser(Guid projectId, Guid userId, string positionCode, Guid assignedBy)`
    *   **Logic:** `INSERT INTO project_assignments ... ON CONFLICT DO NOTHING` (or handle error).
    *   **Validation:** Ensure User exists and is Active.

*   `UnassignUser(Guid projectId, Guid userId, string positionCode, Guid unassignedBy)`
    *   **Logic:** `UPDATE project_assignments SET is_deleted = TRUE ...` (Soft Delete).

---

## 🔌 Phase 2: Service Layer (Business Logic)

### 2.1 Create `TaskAllocationService`
*   **Location:** `Rgt.Space.Core/Services/TaskAllocationService.cs`
*   **Logic:**
    *   Validate "Seat Rule" (User not already in same seat).
    *   Validate "Mandatory Positions" (Check if unassigning a mandatory role -> Warning).
    *   Orchestrate the DAC calls.

---

## 🌐 Phase 3: API Layer (Endpoints)

### 3.1 Create `TaskAllocationController`
*   **Route:** `/api/v1/projects/{projectId}/assignments`

### 3.2 Endpoints
*   `GET /` - Get staffing for this project.
*   `POST /` - Assign a user.
    *   *Body:* `{ userId: Guid, positionCode: string }`
*   `DELETE /{userId}/{positionCode}` - Unassign a user.

---

## 🧪 Phase 4: Validation & Testing

*   **Integration Test:**
    *   Assign John to `TECH_PIC`.
    *   Try to Assign John to `TECH_PIC` again (Should Fail).
    *   Assign John to `TECH_BACKUP` (Should Succeed).
    *   Soft Delete John (User) -> Verify he disappears from `GetProjectAssignments`.
