# Audit Logging Contract

**Version:** 1.0  
**Effective Date:** October 8, 2025  
**Scope:** All tenant operations (reads + writes)  
**Retention:** 90 days per tenant database

---

## 📋 What Gets Audited

### **Coverage**
✅ **ALL MediatR handlers** (queries + commands) - automatic via pipeline behavior  
✅ **ALL FastEndpoints** (direct HTTP endpoints) - automatic via endpoint filter  
✅ **Background jobs** (migrations, nightly tasks) - manual via `IAuditScope`  
❌ **Health checks** - excluded (high volume, low value)  
❌ **Static assets** - excluded (not business operations)

### **Action Taxonomy**

| Category | Actions | Examples |
|----------|---------|----------|
| **Reads** | `{Entity}.Read` | `Sales.Read`, `Customers.Read`, `Products.Read` |
| **Writes** | `{Entity}.Create`, `{Entity}.Update`, `{Entity}.Delete` | `Sales.Create`, `Sales.Update` |
| **Business** | `{Entity}.{Action}` | `Sales.Void`, `Sales.Refund`, `Payments.Capture` |
| **System** | `Tenant.{Action}`, `Config.{Action}` | `Tenant.Onboard`, `Config.Update` |

---

## 🗄️ Schema

### **Storage Location**
- **Per-Tenant:** `[TenantDatabase].dbo.AuditLog` (all tenant operations)
- **Master DB:** `TenantMaster.dbo.AuditLog` (master operations only - future)

### **Table Structure**

```sql
CREATE TABLE dbo.AuditLog (
    -- Identity
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    
    -- Who
    TenantId NVARCHAR(100) NOT NULL,
    UserId NVARCHAR(100) NULL,              -- From JWT 'sub' claim
    ClientId NVARCHAR(100) NULL,            -- Machine-to-machine
    IpAddress NVARCHAR(50) NULL,            -- X-Forwarded-For aware
    UserAgent NVARCHAR(500) NULL,
    
    -- What
    Action NVARCHAR(100) NOT NULL,          -- Action taxonomy (Sales.Read, etc.)
    EntityType NVARCHAR(100) NULL,          -- Sale, Customer, Product
    EntityId NVARCHAR(100) NULL,            -- Specific ID being operated on
    
    -- When/Where
    Timestamp DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CorrelationId NVARCHAR(100) NULL,       -- X-Correlation-Id
    RequestPath NVARCHAR(500) NULL,         -- /api/sales/123
    
    -- Result
    IsSuccess BIT NOT NULL,
    StatusCode INT NULL,                    -- HTTP status code
    ErrorCode NVARCHAR(50) NULL,            -- Business error code
    ErrorMessage NVARCHAR(MAX) NULL,
    DurationMs INT NULL,                    -- Request duration
    
    -- Payloads (compressed + optional encryption)
    RequestData VARBINARY(MAX) NULL,        -- Gzipped JSON
    ResponseData VARBINARY(MAX) NULL,       -- Gzipped JSON
    Delta VARBINARY(MAX) NULL,              -- For writes: before/after diff
    
    -- Metadata
    IdempotencyKey NVARCHAR(100) NULL,      -- For write deduplication
    Source NVARCHAR(50) NOT NULL DEFAULT 'API',  -- API, Job, Migration
    RequestHash NVARCHAR(64) NULL           -- SHA256 of request (optional dedup)
);

-- Indexes
CREATE INDEX IX_AuditLog_Timestamp ON dbo.AuditLog(Timestamp DESC);
CREATE INDEX IX_AuditLog_Correlation ON dbo.AuditLog(CorrelationId);
CREATE INDEX IX_AuditLog_Entity ON dbo.AuditLog(EntityType, EntityId) INCLUDE (Action, StatusCode, Timestamp);
CREATE INDEX IX_AuditLog_Action ON dbo.AuditLog(Action, Timestamp DESC);
CREATE INDEX IX_AuditLog_User ON dbo.AuditLog(UserId, Timestamp DESC);
```

---

## 🔐 Security & Privacy

### **PII Handling**

| Field | Treatment |
|-------|-----------|
| **Credit Card** | Mask all but last 4 digits (`****-****-****-1234`) |
| **Email** | Hash with SHA256 or mask domain (`j***@example.com`) |
| **SSN/Tax ID** | Never log |
| **Phone** | Mask middle digits (`555-***-1234`) |
| **Passwords/Tokens** | Never log (whitelist exclude) |

### **Payload Rules**

1. **Whitelist Only:** Only approved fields are logged
2. **Size Cap:** Max 256KB per payload (compressed)
3. **Compression:** Gzip before storing (VARBINARY)
4. **Encryption (Optional):** AES-256-GCM with app-level keys
5. **Sampling for Reads:** 
   - High-volume reads: 10% sampling (configurable)
   - Writes: 100% always logged
6. **Truncation:** If payload exceeds cap, store marker: `"_truncated": true`

### **Access Control**

| Principal | Permissions |
|-----------|-------------|
| **Application** | INSERT only (no UPDATE, no DELETE) |
| **Admin Users** | SELECT (read audit logs) |
| **Compliance Team** | SELECT + payload decryption keys |
| **Developers** | SELECT (metadata only, no payloads) |
| **DBAs** | Cannot read payloads (encrypted) |

---

## ⏱️ Retention Policy

| Log Type | Retention | Purge Method |
|----------|-----------|--------------|
| **Read Operations** | 90 days | Nightly job per tenant |
| **Write Operations** | 90 days | Nightly job per tenant |
| **Error Logs** | 90 days | Nightly job per tenant |
| **Master DB Logs** | 1 year | Separate purge job |

### **Purge Job**

```sql
-- Runs nightly at 2 AM UTC per tenant database
DELETE FROM dbo.AuditLog
WHERE Timestamp < DATEADD(DAY, -90, SYSDATETIMEOFFSET());

-- Rebuild indexes monthly after large deletes
ALTER INDEX ALL ON dbo.AuditLog REORGANIZE;
```

---

## 🚀 Performance

### **Ingestion Pipeline**

1. **Bounded Queue:** In-memory channel (capacity: 10,000 entries)
2. **Batch Size:** 100-200 inserts per transaction
3. **Flush Interval:** Every 5 seconds or when batch full
4. **Graceful Shutdown:** Flush all pending logs before exit
5. **Backpressure:** If DB is down:
   - Fallback to Serilog file sink
   - Drop payload data (keep metadata)
   - Sample only critical operations

### **Query Performance**

- **Expected Volume:** 10,000-100,000 entries per tenant per day
- **Query SLA:** <500ms for filtered queries (last 7 days)
- **Indexing:** See schema above (Timestamp, CorrelationId, Entity)
- **Partitioning (Future):** Monthly partitions if volume exceeds 10M rows

---

## 📊 Monitoring

### **Audit System Health**

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| **Queue Depth** | <1000 | >5000 (backpressure) |
| **Flush Latency** | <100ms | >1000ms |
| **Failed Writes** | <0.1% | >1% |
| **Payload Compression Ratio** | >5:1 | <3:1 (ineffective) |
| **Disk Growth Rate** | Linear | Exponential (runaway) |

### **Logging**

- **Audit system errors** → Serilog at ERROR level
- **Queue full events** → Serilog at WARNING level
- **Successful batch writes** → Serilog at DEBUG level

---

## 🧪 Testing Requirements

### **Unit Tests**

✅ Audit entry creation with all fields  
✅ PII masking logic  
✅ Payload compression/decompression  
✅ Action taxonomy validation  

### **Integration Tests**

✅ Write to AuditLog table succeeds  
✅ Queue flush on graceful shutdown  
✅ Backpressure mode when DB is down  
✅ Sampling logic for high-volume reads  

### **Manual Tests**

✅ Query audit logs by correlation ID  
✅ Verify PII is masked in payloads  
✅ Check retention job deletes old logs  
✅ Confirm app cannot UPDATE/DELETE logs  

---

## 📝 Compliance Mapping

| Requirement | How We Meet It |
|-------------|----------------|
| **GDPR Right to Access** | Query by UserId → export to CSV/JSON |
| **GDPR Right to Erasure** | Soft delete: set `IsDeleted=1`, hard delete after 90 days |
| **PCI-DSS Audit Trail** | All payment operations logged with Delta |
| **SOC 2 Access Logs** | Who accessed what data, when, from where |
| **HIPAA Audit Logs** | If handling PHI, enable encryption + extend retention |

---

## 🔧 Configuration

### **appsettings.json**

```json
{
  "AuditSettings": {
    "Enabled": true,
    "Storage": {
      "Type": "PerTenantDatabase",
      "TableName": "AuditLog"
    },
    "Payloads": {
      "LogRequests": true,
      "LogResponses": true,
      "Compress": true,
      "Encrypt": false,
      "MaxSizeKB": 256,
      "WhitelistFields": ["id", "tenantId", "status"],
      "MaskFields": ["email", "phone", "cardNumber"]
    },
    "Sampling": {
      "ReadsPercent": 10,
      "WritesPercent": 100
    },
    "Queue": {
      "Capacity": 10000,
      "BatchSize": 200,
      "FlushIntervalSeconds": 5
    },
    "Retention": {
      "Days": 90,
      "PurgeSchedule": "0 2 * * *"
    },
    "Backpressure": {
      "DropPayloads": true,
      "FallbackToFile": true
    }
  }
}
```

---

## 🆘 FAQ

### **Q: Why per-tenant storage instead of centralized?**
**A:** Data isolation, compliance, simpler tenant offboarding (delete entire DB).

### **Q: Why compress payloads?**
**A:** 5-10x space savings, still queryable (decompress on read).

### **Q: Why not log everything?**
**A:** Cost, performance, signal-to-noise ratio. We sample high-volume reads.

### **Q: Can we delete audit logs?**
**A:** Only the nightly purge job can delete (after 90 days). App has INSERT only.

### **Q: What if audit DB is down?**
**A:** Fallback to Serilog file sink, drop payloads, continue serving traffic.

### **Q: How do we export audit logs?**
**A:** Query AuditLog table → export to JSONL/CSV → convert to XLSX if needed.

---

## 📞 Contacts

| Role | Responsibility |
|------|----------------|
| **Engineering Lead** | Audit system implementation & maintenance |
| **Compliance Officer** | Retention policy, PII handling, regulatory alignment |
| **Security Team** | Encryption keys, access control, threat monitoring |
| **DBA Team** | Index tuning, purge jobs, performance optimization |

---

## 🔄 Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-10-08 | Initial contract (reads + writes ready) |

---

**This contract serves as the source of truth for audit logging behavior. Any changes require Engineering Lead + Compliance Officer approval.**

