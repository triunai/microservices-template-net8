
# Multi-Tenant POS Microservice - Gap Analysis & Implementation Roadmap

> **Last Updated:** Session 2 - Operational Readiness Complete  
> **Next Sprint:** Circuit Breakers + Feature Flags

---

## ✅ **COMPLETED - Session 1 (Foundation)**

### **Core Infrastructure**
1. ✅ **FastEndpoints v7** - Sales/GetById endpoint working
2. ✅ **MediatR (CQRS)** - Query handlers with FluentResults
3. ✅ **FluentValidation** - Ready for validation (not yet used)
4. ✅ **Dapper** - Stored procedure execution for reads
5. ✅ **Swagger/OpenAPI** - Full documentation with response types

### **Multi-Tenancy**
6. ✅ **Multi-tenant DB-per-tenant** - Dynamic tenant databases
7. ✅ **Master DB** - TenantMaster with Tenants table
8. ✅ **Tenant Connection Factory** - Master DB lookup + Redis caching
9. ✅ **TenantResolutionMiddleware** - X-Tenant header extraction + log enrichment
10. ✅ **Cache Stampede Protection** - SemaphoreSlim-based concurrency control

### **Observability**
11. ✅ **Serilog** - Structured logging with file + console sinks
12. ✅ **Correlation ID Middleware** - X-Correlation-Id header support
13. ✅ **Log Enrichment** - TenantId, CorrelationId, ClientIP in all logs
14. ✅ **Request Logging** - Automatic HTTP request/response logging

### **Error Handling**
15. ✅ **ProblemDetails (RFC 7807)** - Global exception handler
16. ✅ **Error Catalog** - Centralized error codes (SALE_NOT_FOUND, etc.)
17. ✅ **Correlation Tracking** - All errors include correlationId + traceId

### **Caching & Performance**
18. ✅ **Redis (StackExchange.Redis)** - Distributed caching for connection strings
19. ✅ **Cache TTL Strategy** - 10-minute TTL for tenant connection strings
20. ✅ **Docker Redis** - Local Redis container for development

---

## ✅ **COMPLETED - Session 2 (Operational Readiness)**

### **Health Checks** 🏥
21. ✅ **ASP.NET Core Health Checks** - Master DB + Redis connectivity monitoring
22. ✅ **Health Endpoints** - `/health/live`, `/health/ready`, `/health` with structured JSON
23. ✅ **Tenant-Specific Health** - `/health/tenant/{tenantName}` for individual DB checks
24. ✅ **Swagger Documentation** - All health endpoints visible in Swagger UI

### **Audit Logging** 📝
25. ✅ **AuditLog Table** - Per-tenant audit storage with compression
26. ✅ **Audit Pipeline Behavior** - MediatR automatic audit logging for all queries/commands
27. ✅ **Asynchronous Processing** - Bounded channel with batching (200 entries/batch)
28. ✅ **Payload Compression** - GZip compression for request/response data
29. ✅ **Sampling Strategy** - 100% reads, 100% writes (configurable)
30. ✅ **Graceful Degradation** - Fallback to Serilog if database unavailable

### **Rate Limiting** 🚦
31. ✅ **Per-Tenant Rate Limiting** - 100 requests/10s per tenant with sliding window
32. ✅ **Queue Management** - 10-request queue with graceful backpressure
33. ✅ **429 Responses** - Proper `Retry-After` and rate limit headers
34. ✅ **Load Testing** - k6 tests proving 25% rejection rate under 2x overload
35. ✅ **Multi-Tenant Fairness** - Tenant isolation working (no cross-tenant starvation)

### **API Versioning** 📌
36. ✅ **Path-Based Versioning** - `/api/v1/sales/{id}` endpoint pattern
37. ✅ **Version Headers** - `X-Api-Versions` response header
38. ✅ **Swagger Grouping** - API versions visible in Swagger documentation

---

## 🎯 **PRIORITIZED ROADMAP** (Your Order)

> **Philosophy:** Build operational readiness first, then resilience, then advanced features, and finally auth.

---

## 📋 **SPRINT 1: Resilience & Reliability** (Next Session)

### **1.1 Circuit Breakers & Retries** 🔄 (1-2 hours)
**Priority:** MEDIUM-HIGH - Handle transient failures gracefully

**Tasks:**
- [ ] Install `Polly` (resilience framework)
- [ ] Install `Polly.Extensions.Http`
- [ ] Implement circuit breaker for Master DB queries
  - Open after 5 failures in 30 seconds
  - Half-open after 60 seconds
  - Close after 3 successes
- [ ] Implement circuit breaker for Redis
  - Graceful degradation: fall back to DB if Redis down
- [ ] Implement retry policy with exponential backoff
  - 3 retries: 100ms, 200ms, 400ms
  - Only for transient errors (timeout, connection)
- [ ] Add circuit breaker state to health checks
- [ ] Test failure scenarios

**Success Criteria:**
- API doesn't crash when Redis is down (falls back to DB)
- Circuit breaker opens after repeated failures
- Automatic recovery after services restore

---

## 📋 **SPRINT 2: Advanced Operations**

### **2.1 Feature Flags** 🚩 (1.5-2 hours)
**Priority:** MEDIUM - Toggle features per tenant without redeploy

**Tasks:**
- [ ] Create `FeatureFlags` table in Master DB
  ```sql
  CREATE TABLE FeatureFlags (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId NVARCHAR(100),
    FeatureName NVARCHAR(100),
    IsEnabled BIT,
    Config NVARCHAR(MAX), -- JSON for feature-specific config
    CreatedAt DATETIMEOFFSET,
    UpdatedAt DATETIMEOFFSET,
    UNIQUE(TenantId, FeatureName)
  )
  ```
- [ ] Create `IFeatureFlagService` abstraction
- [ ] Implement with Redis caching (5-minute TTL)
- [ ] Create feature flag evaluation middleware
- [ ] Add admin endpoints:
  - `GET /api/v1/admin/feature-flags`
  - `PUT /api/v1/admin/feature-flags/{tenantId}/{featureName}`
- [ ] Test: toggle feature without redeploy

**Success Criteria:**
- Can enable/disable features per tenant
- Changes take effect within 5 minutes (cache TTL)
- No redeploy needed

---

## 📋 **SPRINT 3: Data Management**

### **3.1 Idempotency** 🔁 (1.5-2 hours)
**Priority:** MEDIUM-HIGH - Prevent duplicate charges in POS

**Prerequisites:** Write operations must be implemented first!

**Tasks:**
- [ ] Create `IdempotencyKeys` table
  ```sql
  CREATE TABLE IdempotencyKeys (
    Key NVARCHAR(100) PRIMARY KEY,
    TenantId NVARCHAR(100),
    Response NVARCHAR(MAX), -- JSON response
    StatusCode INT,
    CreatedAt DATETIMEOFFSET,
    ExpiresAt DATETIMEOFFSET
  )
  ```
- [ ] Create idempotency middleware
- [ ] Extract `Idempotency-Key` from header
- [ ] Check table before processing POST/PUT
- [ ] Store response with 24-72h TTL
- [ ] Return cached response if key exists
- [ ] Test: duplicate POST returns identical response

**Success Criteria:**
- Duplicate POST with same idempotency key returns cached response
- No duplicate sales created
- Idempotency keys expire after 24-72h

---

### **3.2 Multi-Tenant Migrations** 🔄 (2-3 hours)
**Priority:** MEDIUM - Orchestrate schema updates across tenants

**Tasks:**
- [ ] Create `__SchemaVersions` table (per tenant DB)
  ```sql
  CREATE TABLE __SchemaVersions (
    Version INT PRIMARY KEY,
    Description NVARCHAR(500),
    AppliedAt DATETIMEOFFSET,
    AppliedBy NVARCHAR(100)
  )
  ```
- [ ] Create migrations orchestrator service
- [ ] Implement batch processing (10 tenants at a time)
- [ ] Add progress tracking (Redis or Master DB)
- [ ] Implement rollback strategy
- [ ] Test canary deployment (1 tenant first)
- [ ] Document zero-downtime strategy

**Success Criteria:**
- Can apply migration to all tenants
- Progress tracking works
- Rollback plan documented
- Canary deployment tested

---

## 📋 **SPRINT 4: Write Operations & Performance** (Before Auth)

### **4.1 Write Operations** ✍️ (2-3 hours)
**Priority:** MEDIUM - POS needs to create/void/refund sales

**Tasks:**
- [ ] Create `CreateSale` command + handler
  - Validate sale data
  - Insert into Sales + SaleItems tables
  - Return created sale
- [ ] Create `VoidSale` command + handler
  - Mark sale as voided
  - Audit log the action
- [ ] Create `RefundSale` command + handler
  - Create refund sale (negative amounts)
  - Link to original sale
- [ ] Add FluentValidation rules for commands
- [ ] Create FastEndpoints for commands
  - `POST /api/v1/sales`
  - `POST /api/v1/sales/{id}/void`
  - `POST /api/v1/sales/{id}/refund`
- [ ] Test with idempotency keys

**Success Criteria:**
- Can create sales via API
- Can void existing sales
- Can refund sales
- All operations audited

---

### **4.2 Performance Optimization** ⚡ (45-60 min)
**Priority:** HIGH - Current SQL queries are 1-2 seconds (need <100ms for POS)

**Problem:** Load test showed:
- First requests: 1910-2371ms (cold SQL Server)
- Later requests: 100-250ms (under load)
- Final requests: 20-90ms (warmed up)

**Solution A: Response-Level Caching** (Quick win - 15 min)
- [ ] Cache Sale entities in Redis (not just connection strings)
- [ ] Key: `Sale:{tenantId}:{saleId}`
- [ ] TTL: 5 minutes (sales rarely change once created)
- [ ] Expected result: 20-50ms after first hit

**Solution B: Database Optimization** (30-45 min)
- [ ] Add indexes on `Sales.Id`, `SaleItems.SaleId`
- [ ] Test query performance with SQL Server (not LocalDB)
- [ ] Verify connection pooling is configured
- [ ] Consider read replicas (future)

**Success Criteria:**
- k6 load test: avg response time <100ms
- P95 response time <200ms
- No dropped iterations

---

## 📋 **SPRINT 5: Security & Authentication** (FINAL)

### **5.1 Authentication & Authorization** 🔐 (2-3 hours)
**Priority:** MUST-HAVE for production - Secure all endpoints

**Tasks:**
- [ ] Install `Microsoft.AspNetCore.Authentication.JwtBearer`
- [ ] Configure JWT validation
  - OIDC discovery URL (or manual JWKS)
  - Issuer validation
  - Audience validation
- [ ] Extract tenant claim from JWT (`tenantId` or `tenant`)
- [ ] Validate JWT tenant claim matches `X-Tenant` header
  - If mismatch: return 403 Forbidden
- [ ] Create authorization policies
  ```csharp
  builder.Services.AddAuthorization(options =>
  {
      options.AddPolicy("Sales.Read", policy => policy.RequireClaim("permissions", "sales:read"));
      options.AddPolicy("Sales.Write", policy => policy.RequireClaim("permissions", "sales:write"));
      options.AddPolicy("Sales.Void", policy => policy.RequireClaim("permissions", "sales:void"));
      options.AddPolicy("Sales.Refund", policy => policy.RequireClaim("permissions", "sales:refund"));
  });
  ```
- [ ] Add `[Authorize]` to all endpoints
- [ ] Document OIDC integration
- [ ] Test with mock JWT tokens

**Success Criteria:**
- Unauthenticated requests return 401
- JWT without proper claims returns 403
- Tenant claim mismatch returns 403
- Valid JWT with correct claims works

---

### **5.2 Security Baseline** 🛡️ (1-1.5 hours)
**Priority:** HIGH - Production security hardening

**Tasks:**
- [ ] Configure CORS with allowlist
  ```csharp
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("POS", policy =>
          policy.WithOrigins("https://pos.example.com")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
  });
  ```
- [ ] Add HSTS headers (force HTTPS)
- [ ] Add security headers
  - `X-Frame-Options: DENY`
  - `X-Content-Type-Options: nosniff`
  - `X-XSS-Protection: 1; mode=block`
  - `Content-Security-Policy` (optional)
- [ ] Implement PII redaction in Serilog
  - Redact: credit card, SSN, email (optional)
- [ ] Enforce TLS to database
- [ ] Test security headers with browser DevTools

**Success Criteria:**
- CORS blocks unauthorized origins
- Security headers present in all responses
- PII not logged
- TLS enforced

---

## 📊 **Progress Tracking**

| Sprint | Component | Status | Est. Time | Priority |
|--------|-----------|--------|-----------|----------|
| ~~**Session 1**~~ | ~~Health Checks~~ | ✅ **DONE** | 30-45 min | CRITICAL |
| ~~**Session 2**~~ | ~~Audit Logging~~ | ✅ **DONE** | 1-1.5 hrs | MED-HIGH |
| ~~**Session 2**~~ | ~~Rate Limiting~~ | ✅ **DONE** | 1-1.5 hrs | HIGH |
| ~~**Session 2**~~ | ~~API Versioning~~ | ✅ **DONE** | 30-45 min | MEDIUM |
| **1** | Circuit Breakers & Retries | ⏭️ NEXT | 1-2 hrs | MED-HIGH |
| **2** | Feature Flags | ⏸️ TODO | 1.5-2 hrs | MEDIUM |
| **3** | Idempotency | ⏸️ TODO | 1.5-2 hrs | MED-HIGH |
| **3** | Multi-Tenant Migrations | ⏸️ TODO | 2-3 hrs | MEDIUM |
| **4** | Write Operations | ⏸️ TODO | 2-3 hrs | MEDIUM |
| **4** | Performance Optimization | ⏸️ TODO | 45-60 min | HIGH |
| **5** | Authentication & Authorization | ⏸️ TODO | 2-3 hrs | MUST-HAVE |
| **5** | Security Baseline | ⏸️ TODO | 1-1.5 hrs | HIGH |

**Completed:** 4 items (~3.5 hours)  
**Remaining:** 12-16 hours across 5 sprints

---

## 🚀 **Future Enhancements** (Post-MVP)

### **Phase: Azure Production**
- [ ] Deploy to Azure App Service
- [ ] Migrate to Azure SQL Database
- [ ] Migrate to Azure Cache for Redis
- [ ] Set up Application Insights (OTel)
- [ ] Configure auto-scaling
- [ ] Set up Azure Front Door (CDN + WAF)

### **Phase: Advanced Monitoring**
- [ ] OpenTelemetry with OTLP exporter
- [ ] Distributed tracing (trace entire request across services)
- [ ] Custom metrics (sales per tenant, cache hit ratio)
- [ ] Alerting (Slack/Teams on errors)

### **Phase: High Availability**
- [ ] Redis Sentinel (automatic failover)
- [ ] Redis Cluster (horizontal scaling)
- [ ] SQL Always On Availability Groups
- [ ] Multi-region deployment

---

## 📝 **Next Session Action Items**

**COMPLETED IN SESSION 2:**
1. ✅ Health Checks - `/health/live`, `/health/ready`, `/health`, `/health/tenant/{name}`
2. ✅ Audit Logging - Per-tenant storage with async batching and compression
3. ✅ Rate Limiting - Per-tenant sliding window (100 req/10s) with queue
4. ✅ API Versioning - Path-based versioning with v1 endpoints
5. ✅ Load Testing - k6 tests proving rate limiting works under 2x overload

**READY TO START (SPRINT 1):**
1. ⏭️ Circuit Breakers & Retries (Polly for Redis/DB resilience)

**DEFERRED (SPRINT 4):**
- Write operations (needed for full idempotency testing)
- Performance optimization (response caching + database indexes)

**BLOCKED/WAITING:**
- Authentication (waiting until all other features complete)




