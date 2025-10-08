# Serilog Implementation Guide

## ✅ What Was Implemented

### 1. **Serilog Configuration** (`appsettings.json`)
- Console sink with colored output
- File sink with daily rolling logs (30 days retention)
- Log enrichment with CorrelationId and TenantId
- Minimum log levels configured

### 2. **Program.cs Updates**
- Serilog configured early in startup
- Request logging middleware added
- Try-catch wrapper for startup errors
- Proper log flush on shutdown

### 3. **CorrelationIdMiddleware** (NEW)
- Generates or reads `X-Correlation-Id` header
- Enriches ALL logs in the request scope
- Adds correlation ID to response headers
- Stores in `HttpContext.Items` for access

### 4. **TenantResolutionMiddleware** (ENHANCED)
- Now enriches logs with TenantId
- Logs tenant resolution events
- Logs warnings when no tenant header provided
- Stores tenant in `HttpContext.Items`

---

## 📁 File Outputs

### Console Logs
Format: `[HH:mm:ss LEVEL] Message {Properties}`

Example:
```
[14:23:45 INF] Starting up MicroservicesBase.API
[14:23:45 INF] Tenant resolved: 7ELEVEN {"CorrelationId": "abc-123", "TenantId": "7ELEVEN"}
[14:23:45 INF] HTTP GET /api/sales/11111111-1111-1111-1111-111111111111 responded 200 in 45ms
```

### File Logs (./Logs/log-YYYYMMDD.txt)
Format: `YYYY-MM-DD HH:mm:ss.fff zzz [LEVEL] [CorrelationId] [TenantId] Message`

Example:
```
2025-10-07 14:23:45.123 +00:00 [INF] [abc-123] [7ELEVEN] Tenant resolved: 7ELEVEN
2025-10-07 14:23:45.456 +00:00 [INF] [abc-123] [7ELEVEN] HTTP GET /api/sales/11111111-1111-1111-1111-111111111111 responded 200 in 45ms
```

---

## 🧪 How to Test

### 1. Run the Application
```bash
dotnet run --project MicroservicesBase.API
```

### 2. Make a Test Request
```bash
# With tenant header
curl -H "X-Tenant: 7ELEVEN" http://localhost:5000/api/sales/11111111-1111-1111-1111-111111111111

# With custom correlation ID
curl -H "X-Tenant: 7ELEVEN" -H "X-Correlation-Id: my-custom-id" http://localhost:5000/api/sales/11111111-1111-1111-1111-111111111111

# Without tenant header (should log warning)
curl http://localhost:5000/api/sales/11111111-1111-1111-1111-111111111111
```

### 3. Check Console Output
You should see:
```
[14:23:45 INF] Starting up MicroservicesBase.API
[14:23:45 INF] [MASTER CS] Server=RGT-KHUMEREN;Database=TenantMaster;...
[14:23:45 INF] Application startup complete
[14:23:46 INF] Tenant resolved: 7ELEVEN {"CorrelationId": "abc-123", "TenantId": "7ELEVEN"}
[14:23:46 INF] [TENANT CS] 7ELEVEN => Server=RGT-KHUMEREN;Database=Sales7Eleven;...
[14:23:46 INF] HTTP GET /api/sales/11111111-1111-1111-1111-111111111111 responded 200 in 45ms
```

### 4. Check Log Files
```bash
# View today's log file
cat Logs/log-20251007.txt
```

You should see structured logs with CorrelationId and TenantId:
```
2025-10-07 14:23:46.789 +00:00 [INF] [abc-123] [7ELEVEN] Tenant resolved: 7ELEVEN
```

### 5. Check Response Headers
Look for `X-Correlation-Id` in the response:
```
HTTP/1.1 200 OK
Content-Type: application/json
X-Correlation-Id: abc-123
...
```

---

## 🎯 Key Features

### Correlation ID Tracing
- Every request gets a unique correlation ID
- Client can provide their own via `X-Correlation-Id` header
- ID is returned in response headers
- All logs in the request scope include the correlation ID

### Tenant Context Enrichment
- Tenant from `X-Tenant` header is extracted
- All logs in the request scope include the tenant ID
- Tenant resolution is logged for audit trail
- Missing tenant logs a warning (but doesn't block request)

### Middleware Order
```
1. CorrelationIdMiddleware      ← Generates/reads correlation ID
2. UseSerilogRequestLogging     ← Logs HTTP request with enriched context
3. TenantResolutionMiddleware   ← Resolves tenant and enriches logs
4. [Your other middleware]
5. FastEndpoints                ← Routes to endpoints
```

---

## 🔧 Configuration Options

### Change Log Level (appsettings.json)
```json
"MinimumLevel": {
  "Default": "Debug",  // Change to: Verbose, Debug, Information, Warning, Error, Fatal
  ...
}
```

### Change Console Output Template
```json
"WriteTo": [
  {
    "Name": "Console",
    "Args": {
      "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    }
  }
]
```

### Change Log File Retention
```json
"Args": {
  "path": "Logs/log-.txt",
  "rollingInterval": "Day",      // Options: Infinite, Year, Month, Day, Hour, Minute
  "retainedFileCountLimit": 30   // Keep last 30 files
}
```

---

## 📝 Next Steps

Now that Serilog is implemented, you can:

1. **Add ProblemDetails Middleware** (uses Serilog for logging errors)
2. **Add custom log enrichers** (e.g., UserId when auth is implemented)
3. **Add Application Insights sink** (for production monitoring)
4. **Add structured logging in endpoints** (inject `ILogger<T>`)

---

## 🎓 Usage in Your Code

### In Endpoints
```csharp
public sealed class Endpoint(IMediator mediator, ILogger<Endpoint> logger) : EndpointWithoutRequest
{
    public override async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("Processing sale request");
        
        var id = Route<Guid>("id");
        var res = await mediator.Send(new GetSaleById.Query(id), ct);
        
        if (res.IsFailed)
        {
            logger.LogWarning("Sale not found: {SaleId}", id);
            await Send.NotFoundAsync(ct);
            return;
        }
        
        logger.LogInformation("Sale retrieved successfully: {SaleId}", id);
        await Send.OkAsync(res.Value, ct);
    }
}
```

### In Handlers
```csharp
public sealed class Handler(ISalesReadDac dac, ILogger<Handler> logger) : IRequestHandler<Query, Result<SaleResponse>>
{
    public async Task<Result<SaleResponse>> Handle(Query q, CancellationToken ct)
    {
        logger.LogDebug("Querying sale by ID: {SaleId}", q.SaleId);
        
        var data = await dac.GetByIdAsync(q.SaleId, ct);
        
        if (data is null)
        {
            logger.LogWarning("Sale not found in database: {SaleId}", q.SaleId);
            return Result.Fail<SaleResponse>("SALE_NOT_FOUND");
        }
        
        logger.LogInformation("Sale found: {SaleId}, Grand Total: {GrandTotal}", data.Id, data.GrandTotal);
        return Result.Ok(/* map to response */);
    }
}
```

**All logs will automatically include:**
- ✅ CorrelationId
- ✅ TenantId
- ✅ MachineName
- ✅ ThreadId
- ✅ Timestamp
- ✅ Log Level

---

## ✅ Acceptance Criteria Met

- ✅ Logs enriched with tenant + correlationId
- ✅ Structured logging configured
- ✅ Console and file sinks working
- ✅ Request logging with enriched context
- ✅ Correlation ID in response headers
- ✅ Tenant resolution logged

**Ready for next phase: ProblemDetails Middleware!**


