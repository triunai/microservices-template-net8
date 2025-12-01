This is the **Finalized Architecture Reference (Version 1.4)**.

I have synchronized the document with your actual SQL schema and Middleware code to remove the formatting inconsistencies.

***

# 🏛️ Architecture Reference: The ReactPortal Monolith

**Version:** 1.4 (Final Refinement)
**Date:** 2025-11-28
**Status:** AUTHORITATIVE SINGLE SOURCE OF TRUTH
**Target Audience:** AI Agents, Developers, Architects

---

## 🚨 System Context Injection (Read First)

This document defines the **Physical Laws** of the `ReactPortal` system.
When generating code, analyzing schemas, or proposing solutions, **STRICTLY ADHERE** to these constraints.
**Do not offer alternatives** (e.g., "Maybe use Microservices"). We have already decided.

**Current Stack:** PostgreSQL 18 (UUIDv7), ASP.NET Core 8 (Clean Arch), React (Vite/Zustand).
**Schema Strategy:** Logical Monolith (`rgt_space_portal`).
**Auth Strategy:** RS256 (RSA/JWKS) via OIDC.

---

## 1. 🏗️ Database Architecture

### 1.1 Single Database Strategy
**Pattern:** Logical Monolith.
**Database Name:** `rgt_space_portal`.

**Critical Constraints:**
*   **No Multi-Tenant DBs:** We do NOT use "Database-per-Tenant".
*   **Row-Level Isolation:** Tenants share tables. Isolation is enforced via `client_id` FKs.
*   **Global Entities:** Users and Roles are **System-Level**, not Tenant-Level.

### 1.2 Data Scoping Model
| Entity | Scope | Key Column | Note |
| :--- | :--- | :--- | :--- |
| `users` | **GLOBAL** | `id` | Users exist outside of clients. |
| `roles` | **GLOBAL** | `id` | "Project Manager" is a universal concept. |
| `clients` | **GLOBAL** | `id` | The Tenant organizations. |
| `projects` | **CLIENT** | `client_id` | Must belong to a Client. |
| `mappings` | **PROJECT** | `project_id` | Routing configuration. |
| `assignments` | **PROJECT** | `project_id` | Staffing matrix. |

### 1.3 Connection Strings
*   **`PortalDb` (ACTIVE):** The main application database (`rgt_space_portal`).
*   **`RgtAuthPrototype` (ACTIVE):** The external SSO Service Broker database.
*   **`TenantMaster` (LEGACY):** **DO NOT USE.** Artifact from the old POS template.

---

## 2. 🛡️ Domain Boundaries & Business Rules

### 2.1 Identity (IAM)
*   **Scope:** Global.
*   **Constraint:** `users.email` is Globally Unique (Partial Index: `WHERE is_deleted = FALSE`).
*   **Authentication:** Users authenticate against the System, not a specific Tenant.

### 2.2 Tenancy (Catalog)
*   **Structure:** `clients` (1) ↔ (N) `projects`.
*   **Orphan Rule:** Projects **MUST** have `client_id NOT NULL`.
*   **Uniqueness:** Project Codes are unique **per Client** (`UNIQUE (client_id, code)`).
    *   *Example:* Acme can have "POS". TechCorp can have "POS".

### 2.3 Gateway (Routing & Environments)
*   **Structure:** `projects` (1) ↔ (N) `client_project_mappings`.
*   **Safety Net:** Deleting a Mapping **does NOT** delete the Project. This prevents accidental data loss if a URL configuration is removed.
*   **Multi-Environment:** A single Project can have multiple mappings (e.g., `Production`, `UAT`).
*   **URL Logic:** `routing_url` is Globally Unique.
*   **Pattern:** `/{client_code}/{project_code}` (Validated via DB Check Constraint).

### 2.4 Resource Allocation (Staffing)
*   **Structure:** `projects` (1) ↔ (N) `project_assignments`.
*   **Reference Data:** 6 Immutable Position Types (Seeded).
    1. `TECH_PIC`
    2. `TECH_BACKUP`
    3. `FUNC_PIC`
    4. `FUNC_BACKUP`
    5. `SUPPORT_PIC`
    6. `SUPPORT_BACKUP`
*   **Constraint Rule:**
    *   Multiple people CAN hold the same position type (e.g., 2 Support PICs are allowed).
    *   A specific user cannot be assigned the exact same position twice on the same project.
    *   **SQL:** `UNIQUE (project_id, user_id, position_code) WHERE is_deleted = FALSE`.

---

## 3. 🔐 RBAC Architecture

### 3.1 Permission Hierarchy
Permissions are derived via a strictly ordered hierarchy.
1.  **Module:** The Root (e.g., `Projects`).
2.  **Resource:** The Entity (e.g., `ProjectDetail`).
3.  **Action:** The Verb (e.g., `View`, `Edit`, `Delete`).
4.  **Permission Slug:** The Cross-Product.
    *   **Format:** `UPPER_SNAKE_CASE` (e.g., `PROJECTS_EDIT`, `CLIENT_NAV_VIEW`).
    *   **NOT** Dot-Notation.
    *   **Generation:** `Permissions = Resources × Actions` (automatic cross-product).
    *   **Example:** Resource "CLIENT_NAV" × Action "VIEW" = Permission "CLIENT_NAV_VIEW".

### 3.2 Global Roles
*   **Definition:** Roles are defined at the **System Level**.
*   **No Tenancy:** There is no `tenant_id` in the `roles` table.
*   **Assignment Table:** `user_roles`
    *   **Scope:** Global.
    *   **Constraint:** `UNIQUE (user_id, role_id)`.

### 3.3 Overrides & Effective Algo
*   **Overrides Table:** `user_permission_overrides`.
*   **Scope:** Global (No `tenant_id`).
*   **Constraint:** `UNIQUE (user_id, permission_id)`.
*   **Purpose:** Explicit `Allow` or `Deny` for a specific user, bypassing their Role.
*   **Formula:** `Effective = (UserRoles ∪ Overrides_Allow) \ Overrides_Deny`

---

## 4. 🧬 Data Physics

### 4.1 UUID v7 Standard
*   **Mandate:** ALL Primary Keys (`id`) must use `uuid_generate_v7()`.
*   **Prohibited:** `gen_random_uuid()` (v4) for Primary Keys.

### 4.2 The "Zombie Constraint" Protocol
*   **Problem:** Soft Deletes break standard `UNIQUE` constraints.
*   **Solution:** Partial Indexes.
*   **Syntax:** `CREATE UNIQUE INDEX idx_name ON table(col) WHERE is_deleted = FALSE;`

### 4.3 Temporal Governance
*   **Storage:** `TIMESTAMP WITHOUT TIME ZONE` (UTC).
*   **Display:** App converts to `Asia/Kuala_Lumpur`.

---

## 5. ⚠️ Critical Logic Constraints

### 5.1 Deletion Cascade Matrix
| Action | Impact |
| :--- | :--- |
| **Delete Client** | **BLOCKED.** Must delete Projects first. (Safety) |
| **Delete Project** | **CASCADES** to Mappings & Assignments. (Cleanup) |
| **Delete Mapping** | **ISOLATED.** Project remains active. (Safety Net) |
| **Delete User** | **BLOCKED** if they have active Assignments. |

---

## 6. 🛡️ Authentication & Tenant Resolution

### 6.1 SSO Service Broker
*   **Architecture:** Standalone Service.
*   **Role:** Authenticates user via IdP (Azure/Google), returns JWT to Portal.

### 6.2 Token Strategy
*   **Protocol:** **RS256 (RSA Signature)**.
*   **Validation:** Portal validates token signature using Broker's Public Key (JWKS).
*   **Storage:** Access Token (Memory), Refresh Token (HTTP-Only Cookie).

### 6.3 Tenant Resolution Priority
How the system determines the context for a request:
1.  **Priority 1:** JWT Claim `tid` (Tenant ID/Code).
2.  **Priority 2:** HTTP Header `X-Tenant`.
3.  **Priority 3:** Query Parameter `?tenantId=...` (Webhooks only).
*   **Mapping:** The `tid` claim value maps to `clients.code` in the database.
*   **No Tenant Found:** Sets context to "Unknown", logs warning, but continues pipeline (doesn't fail request).

---

## 7. 💻 API & Naming Conventions

*   **DB:** `snake_case` (`is_active`, `project_id`)
*   **JSON:** `camelCase` (`isActive`, `projectId`)
*   **C#:** `PascalCase` (`IsActive`, `ProjectId`)
*   **API Lookups:** ALWAYS query by UUID (`/api/projects/{id}`). NEVER by Name.

---

**END OF ARCHITECTURE CONTEXT**