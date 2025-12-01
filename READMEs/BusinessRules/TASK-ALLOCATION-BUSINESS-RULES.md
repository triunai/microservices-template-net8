# Task Allocation: Business Rules & Logic Matrix

**Version:** 1.0  
**Date:** 2025-12-01  
**Status:** HARDENED & LOCKED 🔒

---

## 🎯 Executive Summary

This document defines the **"God View"** of resource allocation. The Task Allocation module is a **Matrix Grid System** that maps **Projects** (Rows) against **6 Immutable Positions** (Columns).

**Core Directive:**  
"A Project is not just code; it is people. This module ensures every project has the right people in the right seats, with zero ambiguity."

---

## 🎭 The 6 Immutable Positions (The "Cast")

These positions are **Seeded Reference Data**. They cannot be changed, renamed, or deleted by users.

| Code | Name | Type | Mandatory? | UI Behavior |
| :--- | :--- | :--- | :--- | :--- |
| `TECH_PIC` | Technical Lead | **Primary** | **YES** 🚨 | Flag Red if Vacant |
| `TECH_BACKUP` | Technical Backup | Secondary | No | Optional |
| `FUNC_PIC` | Functional Lead | **Primary** | **YES** 🚨 | Flag Red if Vacant |
| `FUNC_BACKUP` | Functional Backup | Secondary | No | Optional |
| `SUPPORT_PIC` | Support Lead | **Primary** | No | Optional |
| `SUPPORT_BACKUP` | Support Backup | Secondary | No | Optional |

> **Vacancy Definition:** A "Vacancy" is strictly defined as **0 records** in the `project_assignments` table for a specific `(project_id, position_code)` tuple.

---

## 🛡️ Assignment Logic & Constraints

### 1. The "Seat" Rule (Uniqueness)
*   **Constraint:** `UNIQUE (project_id, user_id, position_code)`
*   **Rule:** A specific User cannot hold the *exact same seat* twice on the same Project.
*   **Why?** It makes no sense for "John" to be the "Tech PIC" twice.

### 2. The "Scaling" Rule (Multiplicity)
*   **Constraint:** *None* (on the position column).
*   **Rule:** Multiple *different* users **CAN** hold the same position type on the same project.
*   **Example:** A massive project can have **2 Tech PICs**.

### 3. The "Startup" Rule (Cross-Functional)
*   **Constraint:** *None* (on the user column).
*   **Rule:** A single User **CAN** hold *different* positions on the same project.
*   **Example:** "John" can be both `TECH_PIC` and `TECH_BACKUP` (common in small teams).

---

## ⚠️ Guardrails & Safety Protocols

### 1. The "Zombie Employee" Protocol 🧟
*   **Scenario:** User "John" is fired (`status = 'Inactive'`), but he is still assigned to 5 projects.
*   **Risk:** Dashboard shows "Ghost Employees," confusing management.
*   **The Fix (Read-Layer):**
    *   ALL Dashboard queries **MUST** `JOIN users` and explicitly filter `WHERE users.status = 'Active'`.
    *   *Note:* The assignment record remains in the DB (for history), but the UI treats it as invisible.

### 2. The "Infinite Staffing" Guardrail ♾️
*   **Scenario:** A PM accidentally assigns 50 people as `TECH_PIC`.
*   **Risk:** The UI Grid (designed for 1-2 names) explodes/breaks layout.
*   **The Fix (UI-Layer):**
    *   **Soft Limit:** If **> 2 users** are assigned to one slot, the UI displays a **Warning Icon** ⚠️.
    *   *Future:* DB Trigger to block > 3 assignments.

### 3. The "History" Black Hole 🕳️
*   **Rule:** `project_assignments` represents **Current State Only**.
*   **Audit:** Historical staffing data is derived solely from the `audit_logs` table. We do not maintain a temporal `project_assignments_history` table.

---

## 🔥 Deletion Cascade Matrix

| Action | Impact on Assignments | Safety Check |
| :--- | :--- | :--- |
| **Delete Project** | **CASCADES** 🗑️ | **Safe.** If the project is gone, the work is gone. Assignments are wiped. |
| **Delete User** | **BLOCKED** 🛑 | **Critical.** You cannot delete a User if they have active assignments. Must unassign first. |
| **Delete Client** | **BLOCKED** 🛑 | Must delete Projects first. (Indirect protection). |

---

## 🧪 Test Scenarios (Validation)

### Scenario A: The "Double Hat"
```sql
-- John is already Tech PIC. Can he be Tech Backup?
INSERT INTO project_assignments (project_id, user_id, position_code)
VALUES ('proj1', 'john_id', 'TECH_BACKUP');
-- ✅ RESULT: SUCCESS. (Startup Rule)
```

### Scenario B: The "Clone"
```sql
-- John is Tech PIC. Can he be Tech PIC again?
INSERT INTO project_assignments (project_id, user_id, position_code)
VALUES ('proj1', 'john_id', 'TECH_PIC');
-- ❌ RESULT: FAIL. (Seat Rule - Unique Constraint)
```

### Scenario C: The "Firing"
```sql
-- HR deletes John.
DELETE FROM users WHERE id = 'john_id';
-- ❌ RESULT: FAIL. (FK Restrict)
-- Message: "User has active assignments. Please reassign their work first."
```

---

## 📝 SQL Reference

```sql
-- The "God View" Query (Draft)
SELECT 
    p.name as project_name,
    pt.name as position_name,
    u.email as user_email
FROM project_assignments pa
JOIN projects p ON pa.project_id = p.id
JOIN users u ON pa.user_id = u.id
JOIN position_types pt ON pa.position_code = pt.code
WHERE 
    p.is_deleted = FALSE 
    AND pa.is_deleted = FALSE
    AND u.status = 'Active' -- <== ZOMBIE FILTER
ORDER BY p.name, pt.sort_order;
```
