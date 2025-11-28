-- ================================================
-- Security Audit Logging Table - Central Auth Database
-- ================================================
-- Run this script on RgtAuthPrototype database
-- This is the single source of truth for all authentication and authorization audit logs
-- ================================================

USE [RgtAuthPrototype];
GO

-- Create SecurityAudit table in auth schema
CREATE TABLE auth.SecurityAudit (
    -- Identity
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    AuditId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    -- Timestamp
    TimestampUtc    DATETIME2(3) NOT NULL DEFAULT GETUTCDATE(),

    -- Who (Actor)
    TenantId        NVARCHAR(64) NULL,              -- Tenant identifier (nullable for system-wide events)
    UserId          UNIQUEIDENTIFIER NULL,          -- User identifier from JWT 'sub' claim

    -- What (Event Classification)
    EventCategory   NVARCHAR(32) NOT NULL,           -- Security, Auth, Admin, General
    EventType       NVARCHAR(64) NOT NULL,           -- LOGIN_SUCCESS, TOKEN_REUSE_DETECTED, etc.

    -- Result (Outcome)
    IsSuccess       BIT NOT NULL,                    -- Did the operation succeed?
    StatusCode      INT NULL,                        -- HTTP status code (200, 404, 500, etc.)
    ErrorCode       NVARCHAR(64) NULL,               -- Business error code (SSO_001, TENANT_001, etc.)
    ErrorMessage    NVARCHAR(1024) NULL,             -- Error details if failed

    -- Context
    CorrelationId   NVARCHAR(64) NULL,               -- X-Correlation-Id for request tracing
    RequestPath      NVARCHAR(256) NOT NULL,         -- HTTP path (e.g., /auth/login, /api/me)
    HttpMethod       NVARCHAR(16) NOT NULL,          -- GET, POST, PUT, DELETE, etc.
    IpAddress        NVARCHAR(64) NULL,              -- Client IP (X-Forwarded-For aware)
    UserAgent        NVARCHAR(512) NULL,             -- Browser/app user agent

    -- Payloads (JSON strings, scrubbed and truncated)
    RequestPayload   NVARCHAR(MAX) NULL,             -- Request payload as JSON string
    ResponsePayload  NVARCHAR(MAX) NULL,             -- Response payload as JSON string

    -- Performance
    DurationMs       INT NULL,                       -- Request duration in milliseconds

    CONSTRAINT PK_SecurityAudit PRIMARY KEY CLUSTERED (Id ASC)
);
GO

-- ================================================
-- Indexes for Query Performance
-- ================================================

-- Index 1: Query by timestamp (most common - browsing logs)
CREATE NONCLUSTERED INDEX IX_SecurityAudit_TimestampUtc 
    ON auth.SecurityAudit(TimestampUtc DESC);
GO

-- Index 2: Query by correlation ID (trace entire request chain)
CREATE NONCLUSTERED INDEX IX_SecurityAudit_CorrelationId 
    ON auth.SecurityAudit(CorrelationId) 
    WHERE CorrelationId IS NOT NULL;
GO

-- Index 3: Query by event category and type (find all security events, login events, etc.)
CREATE NONCLUSTERED INDEX IX_SecurityAudit_EventCategory_EventType 
    ON auth.SecurityAudit(EventCategory, EventType, TimestampUtc DESC);
GO

-- Index 4: Query by tenant (find all events for a specific tenant)
CREATE NONCLUSTERED INDEX IX_SecurityAudit_TenantId 
    ON auth.SecurityAudit(TenantId, TimestampUtc DESC) 
    WHERE TenantId IS NOT NULL;
GO

-- Index 5: Query by user (find all operations by a specific user)
CREATE NONCLUSTERED INDEX IX_SecurityAudit_UserId 
    ON auth.SecurityAudit(UserId, TimestampUtc DESC) 
    WHERE UserId IS NOT NULL;
GO

-- Index 6: Query by request path (find all events for a specific endpoint)
CREATE NONCLUSTERED INDEX IX_SecurityAudit_RequestPath 
    ON auth.SecurityAudit(RequestPath, TimestampUtc DESC);
GO

-- ================================================
-- Retention Policy - Auto-Delete by Event Category
-- ================================================
-- Schedule this stored procedure to run nightly
-- Different retention periods for Security (365 days) vs General (30 days)
-- ================================================

CREATE PROCEDURE auth.PurgeOldSecurityAuditLogs
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @SecurityCutoffDate DATETIME2(3) = DATEADD(DAY, -365, GETUTCDATE()); -- 1 year for Security events
    DECLARE @GeneralCutoffDate DATETIME2(3) = DATEADD(DAY, -30, GETUTCDATE());   -- 30 days for General events
    DECLARE @RowsDeleted INT;
    DECLARE @TotalDeleted INT = 0;
    
    -- Delete Security events older than 365 days
    WHILE 1 = 1
    BEGIN
        DELETE TOP (10000) FROM auth.SecurityAudit
        WHERE EventCategory = 'Security' 
          AND TimestampUtc < @SecurityCutoffDate;
        
        SET @RowsDeleted = @@ROWCOUNT;
        SET @TotalDeleted = @TotalDeleted + @RowsDeleted;
        
        IF @RowsDeleted = 0
            BREAK;
        
        -- Small delay to reduce load
        WAITFOR DELAY '00:00:01';
    END
    
    IF @TotalDeleted > 0
        PRINT CONCAT('Deleted ', @TotalDeleted, ' Security audit log rows older than ', @SecurityCutoffDate);
    
    SET @TotalDeleted = 0;
    
    -- Delete General events older than 30 days
    WHILE 1 = 1
    BEGIN
        DELETE TOP (10000) FROM auth.SecurityAudit
        WHERE EventCategory = 'General' 
          AND TimestampUtc < @GeneralCutoffDate;
        
        SET @RowsDeleted = @@ROWCOUNT;
        SET @TotalDeleted = @TotalDeleted + @RowsDeleted;
        
        IF @RowsDeleted = 0
            BREAK;
        
        -- Small delay to reduce load
        WAITFOR DELAY '00:00:01';
    END
    
    IF @TotalDeleted > 0
        PRINT CONCAT('Deleted ', @TotalDeleted, ' General audit log rows older than ', @GeneralCutoffDate);
    
    -- Delete Auth and Admin events older than 365 days (same as Security)
    SET @TotalDeleted = 0;
    
    WHILE 1 = 1
    BEGIN
        DELETE TOP (10000) FROM auth.SecurityAudit
        WHERE EventCategory IN ('Auth', 'Admin')
          AND TimestampUtc < @SecurityCutoffDate;
        
        SET @RowsDeleted = @@ROWCOUNT;
        SET @TotalDeleted = @TotalDeleted + @RowsDeleted;
        
        IF @RowsDeleted = 0
            BREAK;
        
        -- Small delay to reduce load
        WAITFOR DELAY '00:00:01';
    END
    
    IF @TotalDeleted > 0
        PRINT CONCAT('Deleted ', @TotalDeleted, ' Auth/Admin audit log rows older than ', @SecurityCutoffDate);
    
    -- Rebuild indexes after large deletes (monthly, on 1st of month)
    IF DAY(GETDATE()) = 1
    BEGIN
        PRINT 'Reorganizing indexes...';
        ALTER INDEX ALL ON auth.SecurityAudit REORGANIZE;
    END
    
    PRINT 'Purge completed successfully.';
END
GO

-- ================================================
-- Security: Restrict Permissions
-- ================================================
-- Grant INSERT only to application user
-- GRANT INSERT ON auth.SecurityAudit TO [YourAppUser];
-- 
-- Grant SELECT to audit/compliance team
-- GRANT SELECT ON auth.SecurityAudit TO [AuditTeam];
-- 
-- Deny UPDATE and DELETE to everyone except DBA/purge job
-- DENY UPDATE, DELETE ON auth.SecurityAudit TO [YourAppUser];
-- GRANT EXECUTE ON auth.PurgeOldSecurityAuditLogs TO [PurgeJobUser];

PRINT 'SecurityAudit table created successfully!';
PRINT 'Indexes created for optimal query performance.';
PRINT 'Purge procedure created for retention policy (Security/Auth/Admin: 365 days, General: 30 days).';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Grant INSERT permission to your application user';
PRINT '2. Grant SELECT permission to audit/compliance team';
PRINT '3. Schedule auth.PurgeOldSecurityAuditLogs to run nightly at 2 AM';
PRINT '4. Configure audit logging in application (AuditSettings in appsettings.json)';
GO

