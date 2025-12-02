# SSO Architecture & Authentication Standards

## 1. System Overview

The RGT Space Portal ecosystem separates **Identity Management** from **Business Logic** to ensure security, scalability, and clean separation of concerns.

### 1.1 Components
*   **SSO Broker Service (`rgt_auth_prototype`):**
    *   **Role:** The centralized Identity Provider (IdP).
    *   **Responsibilities:** Authenticates users, manages global identities, issues tokens (JWT), handles refresh flows, and manages tenant configurations.
    *   **Database:** `rgt_auth_prototype` (Users, Refresh Tokens, SSO Configs).
    *   **Audit Database:** `rgt_auth_audit` (Logs login events, token issuance, security alerts).

*   **ERP Backend (`rgt_space_portal`):**
    *   **Role:** The Relying Party (RP) / Resource Server.
    *   **Responsibilities:** Enforces business rules, manages permissions (RBAC), and serves ERP data. It **NEVER** mints tokens or handles passwords.
    *   **Database:** `rgt_space_portal` (Business Data, Local User Profiles, Permissions).

## 2. Authentication Flow

1.  **Login Request:**
    *   Frontend redirects user to SSO Broker with `tenant=RGT_SPACE_PORTAL`.
2.  **Authentication:**
    *   User logs in at SSO Broker (via Google, Microsoft, or Local credentials).
3.  **Token Issuance:**
    *   SSO Broker issues an **RS256** signed Access Token (JWT) and a Refresh Token.
4.  **API Request:**
    *   Frontend sends JWT in `Authorization: Bearer <token>` header to ERP Backend.
5.  **Validation:**
    *   ERP Backend validates the JWT signature using the SSO Broker's Public Key (fetched via JWKS).
    *   ERP Backend validates `aud` (Audience) and `iss` (Issuer).

## 3. Token Specifications

### 3.1 Access Token (JWT)
*   **Algorithm:** RS256 (Asymmetric RSA).
*   **Lifetime:** Short-lived (e.g., 15 minutes).
*   **Audience (`aud`):** `rgt-space-portal-api`.
*   **Key Discovery:** `/.well-known/jwks.json` (Standard OIDC).

### 3.2 Critical Claims
| Claim | Description | ERP Usage |
| :--- | :--- | :--- |
| `sub` | **Subject ID** (Global UUID). Immutable identifier for the user across the ecosystem. | Used to link to local `users.external_id`. |
| `tid` | **Tenant Key** (e.g., `RGT_SPACE_PORTAL`). Identifies the context of the login. | Validated to ensure user is logging into the correct app. |
| `email` | User's email address. | Used for initial account linking/invitations. |
| `name` | User's display name. | Used for display/auto-provisioning. |

## 4. User Lifecycle Management

### 4.1 Global vs. Local Identity
*   **Global Identity (`rgt_auth_prototype`):** The "Real" user. Holds credentials and global profile.
*   **Local Profile (`rgt_space_portal`):** The "ERP" user. Holds roles, permissions, and business history.
*   **Linkage:** `rgt_space_portal.users.external_id` == `JWT.sub`.

### 4.2 JIT Provisioning (Just-In-Time)
When a valid token is received by the ERP:
1.  **Cache Lookup:** Check `IMemoryCache` for `sub` -> `LocalUserId` mapping.
2.  **DB Lookup:** If miss, query `users` table where `external_id = sub`.
3.  **Onboarding Logic:**
    *   **Match Found:** Log user in, update `last_login_at`.
    *   **No Match:**
        *   Check by `email`. If found, **Link Account** (update `external_id`).
        *   If not found and JIT enabled: **Create User**.
        *   If not found and Invite-Only: **Reject (403)**.

## 5. Security Boundaries

*   **Refresh Tokens:** Handled **exclusively** by the Frontend and SSO Broker. The ERP Backend never sees or touches refresh tokens.
*   **Logout:**
    *   **Front-Channel:** Frontend calls SSO Logout -> Revokes Refresh Token.
    *   **Session End:** ERP relies on Access Token expiration (15m). No back-channel webhook is currently implemented.
*   **Database Isolation:** The ERP Backend **MUST NOT** attempt to connect to `rgt_auth_prototype`. All identity data must be derived from the JWT or synced to the local `users` table.

## 6. Configuration Requirements
*   **Authority:** `https://<sso-broker-url>`
*   **Audience:** `rgt-space-portal-api`
*   **Tenant Key:** `RGT_SPACE_PORTAL`

## 7. API Contract & Headers
For direct interactions with the SSO Broker (e.g., Refresh, Logout), the following headers are mandatory. **Note:** This primarily applies to the Frontend, but is critical for understanding the ecosystem.

| Header | Value | Required For |
| :--- | :--- | :--- |
| `Authorization` | `Bearer <access_token>` | Protected endpoints (e.g., `GET /me`). |
| `X-Tenant` | `RGT_SPACE_PORTAL` | **ALL** endpoints (Login, Refresh, Logout). Used for tenant isolation. |

## 8. Audit Responsibilities
*   **SSO Broker (`rgt_auth_audit`):** Logs **Authentication** events.
    *   *Examples:* "User Logged In", "Token Refreshed", "Invalid Password", "Token Reuse Detected".
*   **ERP Backend (`rgt_space_portal`):** Logs **Authorization & Business** events.
    *   *Examples:* "User Created Order", "User Accessed Restricted Report", "Permission Denied (403)".
    *   *Note:* The ERP does not need to log "User Logged In" as this is handled by the SSO.
