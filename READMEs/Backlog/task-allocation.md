# Task Allocation Module: Requirements & Backlog

**Status:** Draft / Pre-Implementation  
**Date:** 2025-12-01  

## 1. Core Philosophy
*   **Goal:** Provide a "God View" of staffing across all projects.
*   **Structure:** Matrix Grid (Rows: Projects × Columns: 6 Positions).
*   **Vacancy:** Defined as the absence of a record in `project_assignments`.

## 2. The 6 Immutable Positions (Reference Data)
These are hardcoded and cannot be changed.

1.  `TECH_PIC` (Technical Lead) - **MANDATORY** 🚨
2.  `TECH_BACKUP` - *Optional*
3.  `FUNC_PIC` (Functional Lead) - **MANDATORY** 🚨
4.  `FUNC_BACKUP` - *Optional*
5.  `SUPPORT_PIC` (Support Lead) - **MANDATORY** 🚨
6.  `SUPPORT_BACKUP` - *Optional*

> **UI Note:** Mandatory positions must be flagged visually (e.g., Red Border/Icon) if they are vacant. Optional positions can remain empty without warning.

## 3. Agreed Business Rules

### 3.1 Assignment Logic
*   **Uniqueness (The Seat Rule):** A specific User cannot hold the *exact same position* twice on the same Project.
    *   *Constraint:* `UNIQUE (project_id, user_id, position_code)`
*   **Multiplicity (The Scaling Rule):** Multiple *different* users **CAN** hold the same position type on the same project (e.g., A project can have 2 `TECH_PIC`s).
*   **Cross-Functional (The Startup Rule):** A single User **CAN** hold *different* positions on the same project (e.g., John can be both `TECH_PIC` and `TECH_BACKUP`).

### 3.2 "Zombie Employee" Handling (Option A - Query Filter)
*   **The Problem:** If a user is deactivated (`status = 'Inactive'`), their assignment record remains, causing "Ghost Employees" to appear on the dashboard.
*   **The Decision:** Handle this at the **Read Layer** for now.
    *   **Requirement:** All Dashboard queries must `JOIN users` and explicitly filter `WHERE users.status = 'Active'`.
    *   *Future Scope:* Implement Domain Events to automatically `SoftDelete` assignments when a user is deactivated.

### 3.3 "Infinite Staffing" Guardrails
*   **The Problem:** A user could accidentally assign 50 people to one slot, breaking the UI layout.
*   **The Decision:**
    *   **Soft Limit (UI):** Display a warning if **> 2 users** are assigned to a single slot.
    *   **Hard Limit (DB):** (Future) Implement a Trigger to block assignments if count > 3.

### 3.4 History & Audit
*   **The Decision:** The `project_assignments` table represents **Current State** only.
*   **History:** We will rely on the existing `audit_logs` table to reconstruct historical staffing if needed. No complex temporal tables for now.

### 3.5 Client Deletion Safety
*   **The Risk:** Deleting a Client might get blocked by deep FK dependencies.
*   **The Mitigation:**
    *   `clients` -> `projects` (`ON DELETE RESTRICT`) - Must delete projects first.
    *   `projects` -> `assignments` (`ON DELETE CASCADE`) - Deleting project auto-cleans assignments.
    *   `assignments` -> `users` (`ON DELETE RESTRICT`) - Cannot delete a user if they are assigned.
*   **Action Item:** Verify the "Delete Project" flow in integration tests to ensure the cascade works as expected and doesn't hit the User restriction.

## 4. Implementation Checklist (Next Steps)
1.  [ ] **DAC Implementation:** Create `TaskAllocationDac` with Read/Write methods.
2.  [ ] **API Endpoints:** Implement `GET /assignments` (Matrix) and `POST /assignments` (Assign/Unassign).
3.  [ ] **Validation:** Ensure "Mandatory" logic is handled in the Response DTO (e.g., `isVacantCritical: true`).
