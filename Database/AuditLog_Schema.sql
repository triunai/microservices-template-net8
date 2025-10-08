-- ================================================
-- Audit Logging Table - Per-Tenant Database
-- ================================================
-- Run this script on EACH tenant database
-- (Sales7Eleven, SalesBurgerKing, etc.)
-- ================================================

USE [YourTenantDatabase]; -- Replace with actual tenant DB name
GO

-- Create AuditLog table
CREATE TABLE dbo.AuditLog (
    -- Identity
    Id BIGINT IDENTITY(1,1) NOT NULL,
    
    -- Who (Actor)
    TenantId NVARCHAR(100) NOT NULL,
    UserId NVARCHAR(100) NULL,              -- From JWT 'sub' claim (future)
    ClientId NVARCHAR(100) NULL,            -- Machine-to-machine identifier
    IpAddress NVARCHAR(50) NULL,            -- Client IP (X-Forwarded-For aware)
    UserAgent NVARCHAR(500) NULL,           -- Browser/app user agent
    
    -- What (Action)
    [Action] NVARCHAR(100) NOT NULL,        -- Action taxonomy (Sales.Read, Sales.Create, etc.)
    EntityType NVARCHAR(100) NULL,          -- Entity being operated on (Sale, Customer, etc.)
    EntityId NVARCHAR(100) NULL,            -- Specific entity ID
    
    -- When/Where (Context)
    [Timestamp] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CorrelationId NVARCHAR(100) NULL,       -- X-Correlation-Id for request tracing
    RequestPath NVARCHAR(500) NULL,         -- HTTP path (e.g., /api/sales/123)
    
    -- Result (Outcome)
    IsSuccess BIT NOT NULL,                 -- Did the operation succeed?
    StatusCode INT NULL,                    -- HTTP status code (200, 404, 500, etc.)
    ErrorCode NVARCHAR(50) NULL,            -- Business error code (SALE_NOT_FOUND, etc.)
    ErrorMessage NVARCHAR(MAX) NULL,        -- Error details if failed
    DurationMs INT NULL,                    -- Request duration in milliseconds
    
    -- Payloads (Compressed + Optional Encryption)
    RequestData VARBINARY(MAX) NULL,        -- Gzipped JSON of request
    ResponseData VARBINARY(MAX) NULL,       -- Gzipped JSON of response
    Delta VARBINARY(MAX) NULL,              -- For writes: before/after diff (Gzipped JSON)
    
    -- Metadata
    IdempotencyKey NVARCHAR(100) NULL,      -- For write deduplication
    [Source] NVARCHAR(50) NOT NULL DEFAULT 'API',  -- Source: API, Job, Migration
    RequestHash NVARCHAR(64) NULL,          -- SHA256 hash of request (optional deduplication)
    
    CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (Id ASC)
);
GO

-- ================================================
-- Indexes for Query Performance
-- ================================================

-- Index 1: Query by timestamp (most common - browsing logs)
CREATE NONCLUSTERED INDEX IX_AuditLog_Timestamp 
ON dbo.AuditLog([Timestamp] DESC);
GO

-- Index 2: Query by correlation ID (trace entire request chain)
CREATE NONCLUSTERED INDEX IX_AuditLog_Correlation 
ON dbo.AuditLog(CorrelationId);
GO

-- Index 3: Query by entity (find all operations on a specific entity)
CREATE NONCLUSTERED INDEX IX_AuditLog_Entity 
ON dbo.AuditLog(EntityType, EntityId) 
INCLUDE ([Action], StatusCode, [Timestamp]);
GO

-- Index 4: Query by action (find all reads/writes of a type)
CREATE NONCLUSTERED INDEX IX_AuditLog_Action 
ON dbo.AuditLog([Action], [Timestamp] DESC);
GO

-- Index 5: Query by user (find all operations by a specific user)
CREATE NONCLUSTERED INDEX IX_AuditLog_User 
ON dbo.AuditLog(UserId, [Timestamp] DESC) 
WHERE UserId IS NOT NULL;
GO

-- ================================================
-- Retention Policy - 90 Days Auto-Delete
-- ================================================
-- Schedule this stored procedure to run nightly

CREATE PROCEDURE dbo.PurgeOldAuditLogs
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffDate DATETIMEOFFSET = DATEADD(DAY, -90, SYSDATETIMEOFFSET());
    DECLARE @RowsDeleted INT;
    
    -- Delete in batches to avoid long-running transactions
    WHILE 1 = 1
    BEGIN
        DELETE TOP (10000) FROM dbo.AuditLog
        WHERE [Timestamp] < @CutoffDate;
        
        SET @RowsDeleted = @@ROWCOUNT;
        
        IF @RowsDeleted = 0
            BREAK;
        
        -- Log progress
        PRINT CONCAT('Deleted ', @RowsDeleted, ' audit log rows older than ', @CutoffDate);
        
        -- Small delay to reduce load
        WAITFOR DELAY '00:00:01';
    END
    
    -- Rebuild indexes after large deletes (monthly)
    IF DAY(GETDATE()) = 1
    BEGIN
        PRINT 'Reorganizing indexes...';
        ALTER INDEX ALL ON dbo.AuditLog REORGANIZE;
    END
END
GO

-- ================================================
-- Security: Restrict Permissions
-- ================================================
-- Grant INSERT only to application user
-- GRANT INSERT ON dbo.AuditLog TO [YourAppUser];

-- Grant SELECT to audit/compliance team
-- GRANT SELECT ON dbo.AuditLog TO [AuditTeam];

-- Deny UPDATE and DELETE to everyone except DBA/purge job
-- DENY UPDATE, DELETE ON dbo.AuditLog TO [YourAppUser];

PRINT 'AuditLog table created successfully!';
PRINT 'Indexes created for optimal query performance.';
PRINT 'Purge procedure created for 90-day retention.';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Grant INSERT permission to your application user';
PRINT '2. Schedule PurgeOldAuditLogs to run nightly at 2 AM';
PRINT '3. Repeat this script for all tenant databases';
GO

