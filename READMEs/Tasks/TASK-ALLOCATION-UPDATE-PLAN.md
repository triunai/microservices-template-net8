# Task Allocation: Update Assignment & User Management Plan

## 1. Update Assignment (`PUT /api/v1/projects/{projectId}/assignments`)

### Concept
The "Update" operation in our matrix model is essentially a "Move" or "Change Role" operation. Since the primary key of an assignment is composite `(project_id, user_id, position_code)`, changing the `position_code` effectively means creating a new assignment and deleting the old one, or updating the existing row if we treat it as a mutable entity.

However, given our "History Table" approach (soft deletes), an update is best implemented as:
1.  **Soft Delete** the old assignment (mark `is_deleted = TRUE`).
2.  **Insert** the new assignment (with `ON CONFLICT` handling).

### Business Rules
1.  **Idempotency:** Updating a user to the *same* role they already have should be a no-op (200 OK).
2.  **Validation:**
    *   `OldPositionCode` must exist and belong to the user.
    *   `NewPositionCode` must be valid (one of the 6 roles).
    *   `NewPositionCode` must NOT be held by the same user on the same project (unless it's the same as old, which is a no-op).
3.  **Audit:** Must log "User X role changed from A to B on Project Y".

### Implementation Steps
1.  **Endpoint:** `PUT /api/v1/projects/{projectId}/assignments`
    *   Body: `{ userId, oldPositionCode, newPositionCode }`
2.  **Command:** `UpdateAssignment.Command`
3.  **DAC (`TaskAllocationWriteDac`):**
    *   Method: `UpdateAssignmentAsync(projectId, userId, oldPosition, newPosition, updatedBy)`
    *   **Transaction:** This MUST be atomic.
        ```sql
        BEGIN;
        -- 1. Soft Delete Old
        UPDATE project_assignments 
        SET is_deleted = TRUE, deleted_at = NOW(), deleted_by = @UpdatedBy
        WHERE project_id = @Pid AND user_id = @Uid AND position_code = @OldPos;
        
        -- 2. Insert New (or Reactivate if exists as deleted)
        INSERT INTO project_assignments (...) VALUES (...)
        ON CONFLICT (...) DO UPDATE SET is_deleted = FALSE ...;
        COMMIT;
        ```

---

## 2. User Management (Read-Only for Dashboard)

### Concept
To populate the "Staffing Matrix" dropdowns (e.g., "Select a Tech Lead"), the frontend needs a list of available users. Since users are Global (Identity Module), we need endpoints to fetch them.

### Endpoints Required

#### A. Get All Users (`GET /api/v1/users`)
*   **Purpose:** Returns a list of all active users in the system.
*   **Filtering:** Support basic filtering (e.g., `?search=john`).
*   **Response:** `[{ id, displayName, email, avatarUrl, jobTitle }]`
*   **Performance:** Pagination is likely needed if user count > 100.

#### B. Get User Details (`GET /api/v1/users/{userId}`)
*   **Purpose:** Show profile card or details when hovering over a matrix cell.
*   **Response:** Full user details including current assignments (optional, or fetch separately).

### Implementation Steps
1.  **DAC (`UserReadDac`):**
    *   Ensure `GetAllAsync` and `GetByIdAsync` are optimized.
    *   Add `SearchAsync(string term)` for dropdown autocomplete.
2.  **Queries:**
    *   `GetAllUsers.Query`
    *   `GetUserById.Query`
3.  **Endpoints:**
    *   `Rgt.Space.API.Endpoints.Identity.GetUsers`
    *   `Rgt.Space.API.Endpoints.Identity.GetUser`

---

## 3. "God View" Matrix (The Grid)

### Concept
The current `GetProjectAssignments` returns a flat list. The frontend has to pivot this.
We might want a dedicated **Matrix Endpoint** that returns data pre-structured for the grid, OR just stick to the flat list and let the frontend handle it (React is good at this).

**Decision:** Stick to **Flat List** (`GetProjectAssignments`) for now. It's flexible.
*   Frontend maps: `Assignments.filter(a => a.positionCode === 'TECH_PIC')`

---

## 4. Next Session Checklist
- [ ] Implement `UpdateAssignment` (Endpoint, Command, DAC).
- [ ] Implement `GetUsers` (Endpoint, Query) for dropdown population.
- [ ] Verify "Zombie Filter" on `GetUsers` (don't show inactive employees in dropdowns).
