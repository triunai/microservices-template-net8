# SSO Security & ERP Dashboard Requirements

**Version:** 1.0  
**Date:** 2025-11-28  
**Status:** REQUIREMENTS DOCUMENT  
**Purpose:** Define security layers and implementation requirements for SSO Broker and Portal ERP dashboard access

---

## 🎯 Executive Summary

This document outlines the security requirements and implementation approach for:
1. **ERP Dashboard Access** - System-level access to view all clients (not tenant-scoped)
2. **Security Layers** - Multi-layer defense against header manipulation and unauthorized access
3. **"RGTSPACE" System Client** - Special tenant code for ERP dashboard authentication

---

## 🏗️ Architecture: "RGTSPACE" System Client Approach

### Problem Statement
The ERP dashboard needs to show **ALL clients**, not just the tenant from the JWT `tid` claim. However, the SSO Broker requires a `tid` claim for all tokens.

### Solution: System Client Pattern
Use a special "RGTSPACE" tenant code that represents system-level access:

1. **TenantMaster DB**: Add "RGTSPACE" as an active tenant
2. **Portal DB**: Add "RGTSPACE" as a client (maps to `clients.code`)
3. **SSO Broker**: Issues JWT with `tid: "RGTSPACE"` for ERP dashboard logins
4. **Portal**: Detects "RGTSPACE" and grants system-level access (view all clients)

### Benefits
- ✅ No SSO Broker changes required (just another tenant)
- ✅ Clean separation (system vs. regular clients)
- ✅ RBAC still controls access (System Admin role required)
- ✅ Backward compatible (existing client flows unchanged)
- ✅ Clear intent ("RGTSPACE" = system/ERP dashboard context)

---

## 🔒 Security Layers (Defense in Depth)

### Layer 1: JWT Signature (RS256) ✅ Already Implemented
**Protection:** Prevents tampering with `tid` claim

**Current Status:**
- Portal validates JWT signature using SSO Broker's public key (JWKS)
- Frontend cannot fake the `tid` claim in the JWT
- Signature validation happens in `Program.cs` JWT Bearer configuration

**No Action Required** - This layer is already secure.

---

### Layer 2: SSO Broker Authorization ⚠️ Partially Implemented
**Protection:** Validates user-tenant access before issuing JWT

**Current Status:**
- ✅ SSO Broker already validates tenant `isActive` status in TenantMaster DB before issuing JWT
- ❌ Missing: User-tenant authorization check (validates user is authorized for requested tenant)

**Required Implementation in SSO Broker:**

```csharp
// In SSO Broker (pseudo-code)
public async Task<JwtToken> IssueTokenAsync(string userId, string tenantCode)
{
    // 1. Validate tenant exists and is active in TenantMaster DB
    var tenant = await _tenantMasterDb.GetTenantAsync(tenantCode);
    if (tenant == null || !tenant.IsActive)
    {
        throw new UnauthorizedException("Invalid tenant");
    }
    
    // 2. Validate user is authorized for this tenant
    // Check user-tenant mapping table or user's email domain
    var userTenant = await _tenantMasterDb.GetUserTenantAsync(userId, tenantCode);
    if (userTenant == null || !userTenant.IsAuthorized)
    {
        throw new UnauthorizedException("User not authorized for tenant");
    }
    
    // 3. Only then issue JWT with tid claim
    return GenerateJwt(userId, tenantCode);
}
```

**Validation Rules:**
- ✅ Tenant must exist in `TenantMaster.tenants` table (already implemented)
- ✅ Tenant must have `status = 'Active'` (already implemented)
- ❌ User must be authorized for that tenant (check user-tenant mapping) - **MISSING**
- ❌ For "RGTSPACE": Only users with System Admin role should be authorized - **MISSING**

**Action Required:** Implement user-tenant authorization check in SSO Broker before token issuance.

---

### Layer 3: Portal-Side Tenant Validation ❌ Missing (Non-Blocking)
**Protection:** Validates tenant exists and is active in Portal's own `clients` table (not SSO Broker's DB)

**Note:** This is a security hardening layer and can be implemented later. The API works without it, but it adds defense-in-depth by ensuring the tenant code from JWT maps to a valid, active client in Portal DB.

**Required Implementation in `TenantResolutionMiddleware.cs`:**

```csharp
// Add after tenant code resolution (around line 73)
if (!string.IsNullOrWhiteSpace(tenantCode))
{
    // SECURITY: Validate tenant exists and is active in Portal DB
    var clientService = context.RequestServices.GetRequiredService<IClientReadDac>();
    var client = await clientService.GetByCodeAsync(tenantCode, context.RequestAborted);
    
    if (client == null)
    {
        _logger.LogWarning(
            "Invalid tenant code in JWT: {TenantCode} | User: {UserId} | Path: {Path}",
            tenantCode,
            context.User?.FindFirst("sub")?.Value,
            context.Request.Path);
        
        context.Response.StatusCode = 403;
        await context.Response.WriteAsJsonAsync(new { 
            error = "INVALID_TENANT", 
            message = "Tenant not found" 
        }, context.RequestAborted);
        return; // Stop pipeline
    }
    
    // Check if client is active
    // Note: Currently uses status field. Consider adding isActive boolean column for easier querying.
    if (client.Status != "Active") // TODO: Consider migrating to isActive boolean column
    {
        _logger.LogWarning(
            "Inactive tenant in JWT: {TenantCode} | User: {UserId} | Path: {Path}",
            tenantCode,
            context.User?.FindFirst("sub")?.Value,
            context.Request.Path);
        
        context.Response.StatusCode = 403;
        await context.Response.WriteAsJsonAsync(new { 
            error = "TENANT_INACTIVE", 
            message = "Tenant is not active" 
        }, context.RequestAborted);
        return;
    }
    
    // Mark system client for RBAC checks
    if (tenantCode == "RGTSPACE")
    {
        context.Items["IsSystemClient"] = true;
    }
    
    // Only set tenant if validation passes
    context.Items[HttpConstants.ContextKeys.TenantId] = tenantCode;
}
```

**Action Required:** 
- Add tenant validation in `TenantResolutionMiddleware` after tenant code resolution.
- **Optional Migration:** Consider adding `isActive BOOLEAN` column to `clients` table for easier boolean checks (currently uses `status VARCHAR` with 'Active'/'Inactive' values).

---

### Layer 4: X-Tenant Header Restriction ❌ Missing
**Protection:** Prevents header manipulation for authenticated requests

**Current Vulnerability:**
The middleware accepts `X-Tenant` header even for authenticated requests, which could be manipulated by the frontend.

**Required Fix in `TenantResolutionMiddleware.cs`:**

```csharp
// Priority 2: X-Tenant header (ONLY for non-authenticated requests)
if (string.IsNullOrWhiteSpace(tenantCode) && 
    context.User?.Identity?.IsAuthenticated != true) // ← ADD THIS CHECK
{
    tenantCode = context.Request.Headers[HttpConstants.Headers.Tenant].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(tenantCode))
    {
        source = "X-Tenant-header";
        _logger.LogDebug("Tenant resolved from X-Tenant header: {TenantCode}", tenantCode);
    }
}
```

**Security Rule:**
- For **authenticated requests**: Only trust JWT `tid` claim (cryptographically signed)
- For **unauthenticated requests**: Allow `X-Tenant` header (e.g., health checks, public endpoints)

**Action Required:** Add authentication check before accepting `X-Tenant` header.

---

### Layer 5: RBAC Permission Checks ⚠️ Needs Enforcement
**Protection:** Final authorization gate based on user roles

**Required Implementation in Query DACs:**

```csharp
// Example: ClientReadDac.GetAllAsync()
public async Task<IReadOnlyList<ClientReadModel>> GetAllAsync(CancellationToken ct)
{
    var tenantCode = _tenant.Id; // "RGTSPACE" or "ACME" from JWT (cryptographically signed)
    var userId = _httpContext.User.FindFirst("sub")?.Value;
    var isSystemClient = _httpContext.Items["IsSystemClient"] as bool? ?? false;
    
    // SECURITY: Check RBAC permissions
    var permissionService = _httpContext.RequestServices.GetRequiredService<IPermissionService>();
    var permissions = await permissionService.GetUserPermissionsAsync(userId, ct);
    
    if (isSystemClient || tenantCode == "RGTSPACE")
    {
        // System client - requires special permission
        if (!permissions.Contains("CLIENT_NAV_VIEW_ALL"))
        {
            _logger.LogWarning(
                "User {UserId} attempted system access without CLIENT_NAV_VIEW_ALL permission",
                userId);
            throw new ForbiddenException("Insufficient permissions for system access");
        }
        // Return ALL clients (ERP dashboard view)
        return await GetAllClientsFromDbAsync(ct);
    }
    else
    {
        // Regular client - check if user has access
        if (!permissions.Contains("CLIENT_NAV_VIEW"))
        {
            _logger.LogWarning(
                "User {UserId} attempted client access without CLIENT_NAV_VIEW permission",
                userId);
            throw new ForbiddenException("Insufficient permissions");
        }
        // Return only their client
        var client = await GetClientByCodeAsync(tenantCode, ct);
        return client != null ? new[] { client } : Array.Empty<ClientReadModel>();
    }
}
```

**Permission Requirements:**
- `CLIENT_NAV_VIEW_ALL` - Required for "RGTSPACE" system access (System Admin role)
- `CLIENT_NAV_VIEW` - Required for regular client access

**Action Required:** 
1. Add permission checks to all query DACs
2. Ensure System Admin role has `CLIENT_NAV_VIEW_ALL` permission
3. Enforce permissions in endpoint handlers

---

## 📋 Implementation Checklist

### Phase 1: Database Setup
- [ ] Add "RGTSPACE" tenant to `TenantMaster.tenants` table
  ```sql
  INSERT INTO tenants (code, name, status) VALUES 
      ('RGTSPACE', 'RGT Space Portal (System)', 'Active');
  ```

- [ ] Add "RGTSPACE" client to `rgt_space_portal.clients` table
  ```sql
  INSERT INTO clients (code, name, status) VALUES 
      ('RGTSPACE', 'RGT Space Portal (System)', 'Active');
  ```

### Phase 2: SSO Broker Updates
- [ ] Implement user-tenant authorization check before token issuance
- [ ] Validate tenant exists and is active in TenantMaster DB
- [ ] Validate user is authorized for requested tenant
- [ ] For "RGTSPACE": Only authorize users with System Admin role

### Phase 3: Portal Security Fixes
- [ ] Fix `TenantResolutionMiddleware`: Restrict `X-Tenant` header to unauthenticated requests only
- [ ] Add tenant validation in `TenantResolutionMiddleware`: Check tenant exists and is active in Portal DB (can be done later - non-blocking)
- [ ] Add `IsSystemClient` flag when `tid == "RGTSPACE"`
- [ ] Add permission checks to all query DACs
- [ ] Ensure System Admin role has `CLIENT_NAV_VIEW_ALL` permission
- [ ] **Optional:** Consider migrating `clients.status` to `clients.isActive BOOLEAN` column for easier boolean checks

### Phase 4: RBAC Permissions
- [ ] Create `CLIENT_NAV_VIEW_ALL` permission (system-level access)
- [ ] Create `CLIENT_NAV_VIEW` permission (client-level access)
- [ ] Assign `CLIENT_NAV_VIEW_ALL` to System Admin role
- [ ] Update seed data to include these permissions

---

## 🔍 Security Summary Table

| Layer | Protection | Status | Priority |
|-------|------------|--------|----------|
| 1. JWT Signature (RS256) | Prevents tampering with `tid` claim | ✅ Implemented | - |
| 2. SSO Broker Authorization | Validates user-tenant access before issuing JWT | ⚠️ Partially Implemented | **HIGH** |
| 3. Portal Tenant Validation | Validates tenant exists and is active in Portal DB | ❌ Missing (Non-Blocking) | **MEDIUM** |
| 4. X-Tenant Header Restriction | Only allow for unauthenticated requests | ❌ Missing | **MEDIUM** |
| 5. RBAC Permission Checks | Final authorization gate | ⚠️ Needs Enforcement | **HIGH** |

---

## 🚨 Critical Security Rules

1. **Never trust `X-Tenant` header for authenticated requests**
   - Only JWT `tid` claim is cryptographically signed
   - Headers can be manipulated by frontend

2. **Always validate tenant exists and is active**
   - Even if JWT signature is valid, tenant might be deactivated
   - SSO Broker validates TenantMaster DB (already implemented)
   - Portal should validate its own `clients` table (Layer 3 - can be done later)

3. **RBAC is the final gate**
   - Even with valid `tid: "RGTSPACE"`, user must have `CLIENT_NAV_VIEW_ALL` permission
   - System Admin role should have this permission

4. **Log all security violations**
   - Log invalid tenant codes
   - Log inactive tenant access attempts
   - Log permission denial attempts

---

## 📝 Notes

- The JWT signature (RS256) already prevents frontend tampering with the `tid` claim
- The main vulnerabilities are:
  1. Missing authorization checks in SSO Broker
  2. Missing tenant validation in Portal
  3. X-Tenant header accepted for authenticated requests
  4. RBAC not enforced in queries

- The "RGTSPACE" approach is preferred over making `tid` optional because:
  - It's explicit and clear
  - No SSO Broker changes required
  - Maintains backward compatibility
  - Easy to audit (all system access has `tid: "RGTSPACE"`)

---

**END OF DOCUMENT**

