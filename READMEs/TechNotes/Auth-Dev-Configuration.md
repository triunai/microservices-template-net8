# Authentication Configuration & Debugging Guide

## Overview
This document details the specific configuration required to make JWT Authentication work in the **Development Environment**, specifically addressing issues with Self-Signed Certificates and OIDC Discovery.

## 🚨 Critical Production Note
The configurations described below are wrapped in `builder.Environment.IsDevelopment()` checks. 
**Ensure that Production environments run with `ASPNETCORE_ENVIRONMENT=Production`**. 
In Production, the application MUST use standard OIDC Discovery to fetch keys from the SSO Broker (which will have valid, trusted SSL certificates).

---

## 1. The "Hardcoded Key" Bypass (Dev Only)
### Problem
In local development, the Portal API often fails to fetch the OIDC Discovery Document (`/.well-known/openid-configuration`) or the JWKS Keys (`/.well-known/jwks.json`) from the SSO Broker.
**Symptoms:**
- `IDX10500: Signature validation failed. No security keys were provided.`
- Logs show successful `openid-configuration` fetch but no subsequent `jwks.json` fetch.
- Root cause is typically SSL Trust issues with self-signed certificates or internal networking quirks (IPv4 vs IPv6) preventing the `HttpClient` inside the middleware from completing the handshake.

### Solution
We explicitly hardcode the **Public RSA Key** of the SSO Broker into the `JwtBearer` options when running in Development. This bypasses the network call entirely.

**Location:** `Rgt.Space.API/Program.cs`

```csharp
if (builder.Environment.IsDevelopment())
{
    // ... RSA Key Creation ...
    var rsa = RSA.Create();
    rsa.ImportParameters(new RSAParameters { ... }); // Hardcoded Modulus/Exponent
    
    // FORCE THE KEY
    options.TokenValidationParameters.IssuerSigningKey = key;
    
    // DISABLE DISCOVERY
    options.Configuration = new OpenIdConnectConfiguration { ... };
}
```

### Maintenance
If the SSO Broker rotates its Development Keys (e.g., `rsa-2025-11-27` expires), you must:
1. Go to `https://localhost:7012/.well-known/jwks.json`.
2. Copy the new `n` (Modulus) and `e` (Exponent).
3. Update the hardcoded values in `Program.cs`.

---

## 2. Claim Type Mapping
### Problem
By default, ASP.NET Core maps standard OIDC claims (like `sub`, `email`) to verbose .NET XML SOAP types (e.g., `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`).
This causes `OnTokenValidated` checks to fail because they look for `sub` but find the long URL instead.

### Solution
We explicitly **DISABLE** this mapping to keep claims pure.

**Location:** `Rgt.Space.API/Program.cs`

```csharp
.AddJwtBearer(options =>
{
    // ...
    options.MapInboundClaims = false; // KEEPS "sub" as "sub"
    // ...
});
```

**Legacy Note:** The old method `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` is NOT reliable in .NET 8+ because the default handler is now `JsonWebTokenHandler`. Always use `options.MapInboundClaims = false`.

---

## 3. Debugging Tips
If Auth fails again:
1. **Enable PII Logging:** Add `Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;` in `Program.cs`.
2. **Enable Debug Logs:** Set `"Microsoft.AspNetCore.Authentication": "Debug"` in `appsettings.json`.
3. **Check Audience:** Ensure `Auth:Audience` in `appsettings.json` matches the `aud` claim in the token.
