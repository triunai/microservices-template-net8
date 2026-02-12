# TASK-010: Tenant Resolution Middleware Security Fix

**Status:** 🔴 CRITICAL — DO NOW  
**Type:** Security / Tech Debt  
**Owner:** @Khumeren  
**Created:** 2026-01-26  
**Priority:** P0 (Before Feature Flags Implementation)

---

## 🚨 The Problem

### Current Vulnerability

The `TenantResolutionMiddleware` runs **BEFORE** `UseAuthentication()` in the middleware pipeline, making the JWT `tid` claim check effectively **dead code**.

**Evidence from `Program.cs`:**
```csharp
// Current Order (BROKEN)
app.UseMiddleware<CorrelationIdMiddleware>();   // Stage 1
app.UseMiddleware<TenantResolutionMiddleware>(); // Stage 2 ← RUNS HERE
// ... other middleware ...
app.UseAuthentication();                         // Stage 6 ← TOO LATE
app.UseAuthorization();
```

**What happens:**
1. `TenantResolutionMiddleware` checks `context.User?.Identity?.IsAuthenticated`
2. At Stage 2, this is **ALWAYS FALSE** (auth hasn't run yet)
3. So JWT `tid` claim path is **never taken**
4. Tenant is derived from `X-Tenant` header for ALL requests (including authenticated)

### Security Impact

**Attack Vector:** Header Spoofing for Cross-Tenant Data Access

1. Attacker authenticates with valid credentials (their own account)
2. Attacker sets `X-Tenant: VICTIMS_TENANT` header
3. `SalesReadDac` uses `ITenantProvider.Id` to resolve connection string
4. Attacker queries victim tenant's Sales database

**Blast Radius:**
- `SalesReadDac` — **VULNERABLE** (uses tenant for DB connection routing)
- Portal Routing DACs — **NOT AFFECTED** (use explicit `client_id` from route/body)
- Feature Flags — **NOT AFFECTED** (TASK-009 uses explicit `clientId`, not tenant)

---

## ✅ The Solution: Two-Phase Tenant Resolution

### Phase 1: Pre-Auth (Header Hint)
For unauthenticated requests (rate limiting, CORS, logging context):
- Read from `X-Tenant` header or query param
- Set as "provisional" tenant context

### Phase 2: Post-Auth (JWT Authority)
For authenticated requests:
- Read `tid` claim from validated JWT
- **Override** any header-derived tenant
- If JWT has no `tid` claim → treat as "NO_TENANT" (safe default)

### Middleware Pipeline (Fixed)

```csharp
// FIXED Order
app.UseMiddleware<CorrelationIdMiddleware>();      // 1. Correlation ID
app.UseMiddleware<TenantHintMiddleware>();         // 2. Provisional tenant from header (for rate limiting)
app.UseRateLimiter();                              // 3. Rate limiting (uses provisional tenant)
app.UseCors("AllowAll");
app.UseSerilogRequestLogging();
app.UseAuthentication();                           // 4. JWT validation
app.UseMiddleware<TenantAuthorityMiddleware>();    // 5. Override tenant from JWT (NEW!)
app.UseMiddleware<PermissionLoadingMiddleware>();
app.UseAuthorization();
```

---

## 📐 Design Specification

### Option A: Split Middleware (Recommended)

**Create two separate middleware:**

#### `TenantHintMiddleware` (Pre-Auth)
```csharp
public class TenantHintMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Read from header/query only (for rate limiting, logging)
        var tenantCode = context.Request.Headers["X-Tenant"].FirstOrDefault()
                      ?? context.Request.Query["tenantId"].FirstOrDefault();
        
        if (!string.IsNullOrEmpty(tenantCode))
        {
            context.Items["TenantHint"] = tenantCode;  // Provisional
            context.Items["TenantSource"] = "Header";
        }
        
        await _next(context);
    }
}
```

#### `TenantAuthorityMiddleware` (Post-Auth)
```csharp
public class TenantAuthorityMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        string? tenantCode = null;
        string source = "None";
        
        // Priority 1: JWT claim (for authenticated requests)
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            tenantCode = context.User.FindFirst("tid")?.Value;
            source = "JWT";
            
            // Log if header differs from JWT (potential attack attempt)
            var headerHint = context.Items["TenantHint"] as string;
            if (!string.IsNullOrEmpty(headerHint) && headerHint != tenantCode)
            {
                _logger.LogWarning(
                    "Tenant mismatch: Header={HeaderTenant}, JWT={JwtTenant}. Using JWT.",
                    headerHint, tenantCode);
            }
        }
        
        // Priority 2: Fall back to header hint (for unauthenticated requests ONLY)
        if (string.IsNullOrEmpty(tenantCode))
        {
            tenantCode = context.Items["TenantHint"] as string;
            source = string.IsNullOrEmpty(tenantCode) ? "None" : "Header";
        }
        
        // Set final tenant context
        tenantProvider.SetTenant(tenantCode ?? "Unknown");
        context.Items["TenantCode"] = tenantCode;
        context.Items["TenantSource"] = source;
        
        await _next(context);
    }
}
```

### Option B: Single Middleware with Deferred Resolution

Keep one middleware but defer JWT check until after auth:

```csharp
public class TenantResolutionMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        // Set provisional from header (for rate limiting)
        var headerTenant = context.Request.Headers["X-Tenant"].FirstOrDefault();
        context.Items["TenantHint"] = headerTenant;
        
        // Register callback to finalize after auth
        context.Response.OnStarting(() =>
        {
            // This is too late for request processing
            return Task.CompletedTask;
        });
        
        await _next(context);
    }
}
```

**Problem with Option B:** The tenant needs to be finalized BEFORE downstream middleware/handlers run, not in `OnStarting`. This approach doesn't work well.

**Recommendation:** Use **Option A** (Split Middleware).

---

## 📝 Implementation Plan

### Phase 1 — Create New Middleware

- [ ] Create `TenantHintMiddleware.cs` (pre-auth, header/query only)
- [ ] Create `TenantAuthorityMiddleware.cs` (post-auth, JWT authority)
- [ ] Update `ITenantProvider` interface if needed (add `SetTenant` method or use scoped instance)
- [ ] Add logging for tenant source tracking

### Phase 2 — Update Pipeline

- [ ] Modify `Program.cs` middleware order
- [ ] Remove or rename old `TenantResolutionMiddleware`
- [ ] Test that rate limiting still works with provisional tenant

### Phase 3 — Verification

- [ ] **Security Test:** Authenticated request with mismatched header
  - Verify JWT tenant takes precedence
  - Verify warning is logged
- [ ] **Regression Test:** Unauthenticated webhook with `?tenantId=...`
  - Verify header/query still works for unauthenticated
- [ ] **SalesReadDac Test:** Verify correct tenant DB is queried

### Phase 4 — Cleanup

- [ ] Remove deprecated middleware file
- [ ] Update Architecture-Current-State.md (remove from Section 8.2 debt)
- [ ] Update any documentation referencing old middleware

---

## 🧪 Test Cases

### Security Test Matrix

| Request Type | X-Tenant Header | JWT `tid` Claim | Expected Tenant | Log |
|--------------|-----------------|-----------------|-----------------|-----|
| Unauthenticated | `ACME` | N/A | `ACME` | Source: Header |
| Unauthenticated | (none) | N/A | `Unknown` | Source: None |
| Authenticated | `ACME` | `RGT_SPACE_PORTAL` | `RGT_SPACE_PORTAL` | Source: JWT |
| Authenticated | `ATTACKER` | `RGT_SPACE_PORTAL` | `RGT_SPACE_PORTAL` | ⚠️ Mismatch warning |
| Authenticated | (none) | `RGT_SPACE_PORTAL` | `RGT_SPACE_PORTAL` | Source: JWT |
| Authenticated | `ACME` | (no claim) | `ACME` | Source: Header (degraded) |

### Boundary Cases

- [ ] Expired JWT (treated as unauthenticated)
- [ ] Malformed JWT (treated as unauthenticated)
- [ ] Empty `tid` claim (treat as no claim)

---

## 🎯 Success Criteria

| Criterion | Verification |
|-----------|--------------|
| ✅ JWT tenant takes precedence for authenticated requests | Security test passes |
| ✅ Header spoofing logged as warning | Log contains mismatch alert |
| ✅ Unauthenticated requests still work with header | Webhook test passes |
| ✅ Rate limiting uses provisional tenant | Rate limit partitions work |
| ✅ SalesReadDac uses correct tenant | Sales query test passes |
| ✅ No regression in existing functionality | Full regression suite passes |

---

## ⚠️ Risks & Mitigations

### Risk 1: Rate Limiting Breaks
**Concern:** Rate limiter runs before auth, needs tenant context.  
**Mitigation:** `TenantHintMiddleware` provides provisional tenant from header.

### Risk 2: Existing Clients Rely on Header
**Concern:** Some clients/tests may set header instead of using JWT.  
**Mitigation:** 
- For authenticated: JWT wins, log warning
- For unauthenticated: Header still works (backward compatible)

### Risk 3: JWT Missing `tid` Claim
**Concern:** Some token paths might not have `tid`.  
**Mitigation:** Fall back to header with degraded logging. Document which token types need `tid`.

---

## 📚 References

- [Architecture-Current-State.md Section 6.3](../Architecture/Architecture-Current-State.md)
- [TenantResolutionMiddleware.cs](../../Rgt.Space.API/Middleware/TenantResolutionMiddleware.cs)
- [Program.cs Middleware Pipeline](../../Rgt.Space.API/Program.cs)
- [SSO-Architecture.md](../SSO-Architecture.md) — JWT claims specification

---

## 🔗 Dependencies

- **Blocks:** TASK-009 (Feature Flags) should ideally wait for this fix
- **But:** TASK-009 explicitly uses explicit `clientId`, so it's NOT dependent on this
- **Recommended:** Fix this first, but TASK-009 can proceed in parallel

---

**END OF TASK-010**
