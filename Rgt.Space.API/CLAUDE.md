# Rgt.Space.API — Presentation Layer

FastEndpoints-based API with dual JWT auth, multi-tenant middleware pipeline, and RFC 7807 error responses.

## FastEndpoints Patterns

### Pattern A — Request + MediatR (most common)
```csharp
public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser)
    : Endpoint<CreateUserRequest>
{
    public override void Configure()
    {
        Post("/api/v1/users");
        Summary(s => { s.Summary = "Create a new user"; s.Response<CreateUserResponse>(201, "..."); });
        Tags("User Management");
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var command = new CreateUserCommand(..., currentUser.Id);
        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendCreatedAtAsync<GetUser.Endpoint>(
            new { userId = result.Value }, response, cancellation: ct);
    }
}
```

### Pattern B — No request (query endpoints)
```csharp
public sealed class Endpoint : EndpointWithoutRequest<DashboardStatsResponse>
{
    public override void Configure() { Get("/api/v1/dashboard/stats"); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var stats = await _dac.GetStatsAsync(ct);
        await Send.OkAsync(stats, ct);
    }
}
```

### Pattern C — Request + Response generic
```csharp
public sealed class Endpoint(IMediator mediator) : Endpoint<LoginRequest, LoginResponse> { ... }
```

### Error Handling in Endpoints
Always: `result.IsFailed` → `result.ToProblemDetails(HttpContext)` → `SendAsync(problemDetails, status)`.
Never throw exceptions from endpoints — use FluentResults.

### Response Methods
- `Send.OkAsync(data, ct)` — 200
- `Send.ResponseAsync(data, statusCode, ct)` — custom status
- `HttpContext.Response.SendCreatedAtAsync<Endpoint>(routeValues, data)` — 201 + Location header

## Endpoint Folder Structure

```
Endpoints/
├── Auth/Login/
├── Audit/DecodeAuditPayload/
├── Dashboard/GetStats/
├── Debug/ComboBreak/
├── Health/{GetHealth,GetLiveness,GetReadiness,GetTenantHealth}/
├── Identity/{CreateUser,GetUser,GetUsers,UpdateUser,DeleteUser,
│             AssignRole,UnassignRole,GetUserRoles,
│             GrantPermission,RevokePermission,GetUserPermissions}/
├── PortalRouting/{CreateClient,GetClientById,GetAllClients,UpdateClient,DeleteClient,
│                  CreateProject,GetProjectById,GetAllProjects,UpdateProject,DeleteProject,
│                  CreateMapping,UpdateMapping,DeleteMapping,GetAllMappings,
│                  GetProjectsByClient}/
├── Roles/{GetRoles,GetRole,CreateRole,UpdateRole,DeleteRole}/
├── Sales/GetById/
├── TaskAllocation/{AssignUser,UnassignUser,UpdateAssignment,
│                   GetProjectAssignments,GetStaffingMatrix}/
└── Features/{GetFeatures,GetFeatureById,CreateFeature,UpdateFeature,
              DeleteFeature,UpsertClientFeature,SetUserOverride,ClearUserOverride}/
```

Convention: `Endpoints/[Domain]/[Action]/Endpoint.cs`

Request/Response DTOs live in **Core** (`Domain/Contracts/[Domain]/`), not in API.

## Route Conventions

```
POST   /api/v1/users                              Create
GET    /api/v1/users/{userId:guid}                 Get single (typed GUID)
PUT    /api/v1/users/{userId:guid}                 Update
DELETE /api/v1/users/{userId:guid}                 Delete
POST   /api/v1/users/{userId}/roles                Assign role (sub-resource)
GET    /api/v1/dashboard/stats                     Aggregation
POST   /api/v1/auth/login                          Special (AllowAnonymous)

# Feature Flags (TASK-009/010/011)
GET    /api/v1/features                             List all (FEATURES.LIST.VIEW)
GET    /api/v1/features/{featureId:guid}            Get single (FEATURES.LIST.VIEW)
POST   /api/v1/features                             Create (FEATURES.GLOBAL.EDIT)
PUT    /api/v1/features/{featureId:guid}            Update (FEATURES.GLOBAL.EDIT)
DELETE /api/v1/features/{featureId:guid}            Soft delete (FEATURES.GLOBAL.EDIT)
PUT    /api/v1/features/{featureId}/clients/{clientId}  Upsert subscription (FEATURES.CLIENT.EDIT)
POST   /api/v1/features/{featureId}/user-overrides  Set override (FEATURES.OVERRIDE.INSERT)
DELETE /api/v1/features/{featureId}/user-overrides/{userId}  Clear (FEATURES.OVERRIDE.DELETE)
GET    /api/v1/features/{featureId}/clients         Client subscriptions (FEATURES.LIST.VIEW)
GET    /api/v1/clients/{clientId}/features          Features by client (FEATURES.LIST.VIEW)
GET    /api/v1/features/{featureId}/user-overrides  User overrides by feature (FEATURES.LIST.VIEW)
GET    /api/v1/users/{userId}/feature-overrides     Overrides by user (FEATURES.LIST.VIEW)
GET    /api/v1/features/evaluate/{featureCode}      Single eval (AllowAnonymous)
GET    /api/v1/features/evaluate                    Bulk eval (AllowAnonymous)
```

All routes hardcoded as `/api/v1/...`. API versioning configured but only v1 exists.

## Middleware Pipeline (EXACT ORDER — updated TASK-014)

```
 1. UseExceptionHandler()              → GlobalExceptionHandler (catches all, ProblemDetails)
 2. CorrelationIdMiddleware            → Generates/extracts X-Correlation-Id, pushes to LogContext
 3. ComboBreakHeadersMiddleware        → DEV ONLY: adds X-Checkpoint-* headers to 5xx responses
 4. UseRateLimiter()                   → IP-based sliding window (1000 req / 10s)
 5. RateLimitHeadersMiddleware         → Adds X-RateLimit-* info headers to all responses
 6. UseCors("Default")                 → Env-conditional (AllowAny dev, whitelist prod)
 7. UseSerilogRequestLogging()         → Enriches with CorrelationId, TenantId, ClientIP
 8. UseAuthentication()                → Dual JWT validation (SSO + Local via MultiScheme)
 9. TenantResolutionMiddleware         → JWT tid (authoritative, ToUpperInvariant) → X-Tenant header (fallback). Case-insensitive comparison, mismatch → 403
10. PermissionLoadingMiddleware        → Loads DB permissions → adds as "permissions" claims (1-min cache)
11. UseAuthorization()                 → Standard ASP.NET authz
12. UseFastEndpoints()                 → Endpoint routing + built-in exception handling
13. UseSwaggerGen()                    → Swagger UI
14. NotFoundMiddleware                 → Converts 404 → 400 ProblemDetails (anti-probing)
15. MapHealthChecks (x3)               → /health/live, /health/ready, /health
```

## Auth Setup

### Dual JWT Schemes
| Scheme | Algorithm | Source | Issuer |
|--------|-----------|--------|--------|
| `SsoBearer` | RSA-SHA256 | External SSO broker (OIDC discovery) | OIDC Authority |
| `LocalBearer` | HMAC-SHA256 | `/api/v1/auth/login` | `rgt-space-portal` |

**MultiScheme selection**: Reads token, checks issuer — `"rgt-space-portal"` → LocalBearer, else → SsoBearer.

**SSO OnTokenValidated**: Extracts `sub`, `email`, `name`, `ext_provider` → calls `SyncOrGetUserAsync` → adds `x-local-user-id` claim. Uses `ext_provider` claim (not issuer-based detection) to determine SSO provider.

**Local OnTokenValidated**: `sub` claim IS the local user ID → adds `x-local-user-id` claim.

### Permission Format
`MODULE.RESOURCE.ACTION` — e.g., `TASK_ALLOCATION.MEMBERS_DIST.VIEW`

Loaded by `PermissionLoadingMiddleware` from `IUserReadDac.GetPermissionsAsync(userId)`, cached 1 minute.

## Health Checks

| Endpoint | Probe Type | Checks | Use Case |
|----------|-----------|--------|----------|
| `/health/live` | Liveness | None (process alive) | K8s liveness |
| `/health/ready` | Readiness | PostgreSQL (tagged "ready") | K8s readiness |
| `/health` | General | PostgreSQL + Redis | Monitoring |

Redis is NOT required for readiness — API works without it (connection strings use IMemoryCache).

## ProblemDetails (RFC 7807)

All error responses include: `type`, `title`, `status`, `detail`, `instance`, `correlationId`, `tenantId`, `traceId`, `checkpointCurrent`, `checkpointLast`, `errorCode`, `timestamp`.

Extension method: `result.ToProblemDetails(HttpContext)` converts FluentResults failures.

## Rate Limiting

IP-based sliding window: 1000 requests / 10 seconds, 2 segments (5s each), queue 10 overflow.
429 response includes `Retry-After: 10` header and ProblemDetails body.

## Dev Startup

```bash
dotnet run --project Rgt.Space.API
```

Swagger UI available at: `/swagger` (auto-generated from FastEndpoints)
