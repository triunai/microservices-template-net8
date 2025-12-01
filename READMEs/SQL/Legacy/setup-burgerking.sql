-- ================================================
-- Setup BURGERKING Tenant Database
-- ================================================
-- This script sets up the SalesBurgerKing database with:
-- 1. Sales and SaleItems tables
-- 2. GetSaleWithItems stored procedure
-- 3. Seed test data (same structure as 7ELEVEN)
-- 4. AuditLog table for audit logging
-- ================================================

USE SalesBurgerKing;
GO

-- ================================================
-- 1. Create Sales Table
-- ================================================
IF OBJECT_ID('dbo.Sales') IS NULL
BEGIN
  CREATE TABLE dbo.Sales (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId NVARCHAR(100) NOT NULL,
    StoreId NVARCHAR(50) NOT NULL,
    RegisterId NVARCHAR(50) NOT NULL,
    ReceiptNumber NVARCHAR(50) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    NetTotal DECIMAL(18,2) NOT NULL,
    TaxTotal DECIMAL(18,2) NOT NULL,
    GrandTotal DECIMAL(18,2) NOT NULL
  );
  CREATE UNIQUE INDEX UX_Sales_Receipt ON dbo.Sales(StoreId, RegisterId, ReceiptNumber);
  PRINT '✅ Sales table created';
END
ELSE
BEGIN
  PRINT '⚠️ Sales table already exists';
END
GO

-- ================================================
-- 2. Create SaleItems Table
-- ================================================
IF OBJECT_ID('dbo.SaleItems') IS NULL
BEGIN
  CREATE TABLE dbo.SaleItems (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    SaleId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.Sales(Id),
    Sku NVARCHAR(50) NOT NULL,
    Qty INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL
  );
  PRINT '✅ SaleItems table created';
END
ELSE
BEGIN
  PRINT '⚠️ SaleItems table already exists';
END
GO

-- ================================================
-- 3. Create GetSaleWithItems Stored Procedure
-- ================================================
IF OBJECT_ID('dbo.GetSaleWithItems') IS NULL
BEGIN
  EXEC('
    CREATE PROCEDURE dbo.GetSaleWithItems @SaleId UNIQUEIDENTIFIER AS
    BEGIN
      SET NOCOUNT ON;
      
      -- Result set 1: Header
      SELECT Id,TenantId,StoreId,RegisterId,ReceiptNumber,CreatedAt,NetTotal,TaxTotal,GrandTotal
      FROM dbo.Sales WITH (NOLOCK) WHERE Id = @SaleId;

      -- Result set 2: Items
      SELECT Sku,Qty,UnitPrice
      FROM dbo.SaleItems WITH (NOLOCK) WHERE SaleId = @SaleId;
    END
  ');
  PRINT '✅ GetSaleWithItems stored procedure created';
END
ELSE
BEGIN
  PRINT '⚠️ GetSaleWithItems stored procedure already exists';
END
GO

-- ================================================
-- 4. Seed Test Data (BURGERKING)
-- ================================================
DECLARE @SaleId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

IF NOT EXISTS (SELECT 1 FROM dbo.Sales WHERE Id = @SaleId)
BEGIN
  -- Insert sale header
  INSERT dbo.Sales(Id,TenantId,StoreId,RegisterId,ReceiptNumber,CreatedAt,NetTotal,TaxTotal,GrandTotal)
  VALUES (@SaleId,'BURGERKING','STORE001','REG01','RCP-BK-001',SYSDATETIMEOFFSET(),15.00,0.90,15.90);

  -- Insert sale items (Burger King menu items)
  INSERT dbo.SaleItems(SaleId,Sku,Qty,UnitPrice)
  VALUES (@SaleId,'SKU-WHOPPER',1,8.00),
         (@SaleId,'SKU-FRIES',1,3.50),
         (@SaleId,'SKU-COKE',1,3.50);
         
  PRINT '✅ Seed data inserted (1 sale with 3 items)';
END
ELSE
BEGIN
  PRINT '⚠️ Seed data already exists';
END
GO

-- ================================================
-- 5. Create AuditLog Table
-- ================================================
IF OBJECT_ID('dbo.AuditLog') IS NULL
BEGIN
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
  PRINT '✅ AuditLog table created';
END
ELSE
BEGIN
  PRINT '⚠️ AuditLog table already exists';
END
GO

-- ================================================
-- 6. Create AuditLog Indexes
-- ================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_Timestamp' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
  CREATE NONCLUSTERED INDEX IX_AuditLog_Timestamp ON dbo.AuditLog([Timestamp] DESC);
  PRINT '✅ Index: IX_AuditLog_Timestamp';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_Correlation' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
  CREATE NONCLUSTERED INDEX IX_AuditLog_Correlation ON dbo.AuditLog(CorrelationId);
  PRINT '✅ Index: IX_AuditLog_Correlation';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_Entity' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
  CREATE NONCLUSTERED INDEX IX_AuditLog_Entity ON dbo.AuditLog(EntityType, EntityId) INCLUDE ([Action], StatusCode, [Timestamp]);
  PRINT '✅ Index: IX_AuditLog_Entity';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_Action' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
  CREATE NONCLUSTERED INDEX IX_AuditLog_Action ON dbo.AuditLog([Action], [Timestamp] DESC);
  PRINT '✅ Index: IX_AuditLog_Action';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_User' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
  CREATE NONCLUSTERED INDEX IX_AuditLog_User ON dbo.AuditLog(UserId, [Timestamp] DESC) WHERE UserId IS NOT NULL;
  PRINT '✅ Index: IX_AuditLog_User';
END
GO

-- ================================================
-- 7. Create Purge Stored Procedure
-- ================================================
IF OBJECT_ID('dbo.PurgeOldAuditLogs') IS NULL
BEGIN
  EXEC('
    CREATE PROCEDURE dbo.PurgeOldAuditLogs
    AS
    BEGIN
      SET NOCOUNT ON;
      DECLARE @CutoffDate DATETIMEOFFSET = DATEADD(DAY, -90, SYSDATETIMEOFFSET());
      DECLARE @RowsDeleted INT;
      
      -- Delete in batches to avoid long-running transactions
      WHILE 1 = 1
      BEGIN
        DELETE TOP (10000) FROM dbo.AuditLog WHERE [Timestamp] < @CutoffDate;
        SET @RowsDeleted = @@ROWCOUNT;
        IF @RowsDeleted = 0 BREAK;
        PRINT CONCAT(''Deleted '', @RowsDeleted, '' audit log rows older than '', @CutoffDate);
        WAITFOR DELAY ''00:00:01'';
      END
      
      -- Rebuild indexes after large deletes (monthly)
      IF DAY(GETDATE()) = 1
      BEGIN
        PRINT ''Reorganizing indexes...'';
        ALTER INDEX ALL ON dbo.AuditLog REORGANIZE;
      END
    END
  ');
  PRINT '✅ PurgeOldAuditLogs stored procedure created';
END
ELSE
BEGIN
  PRINT '⚠️ PurgeOldAuditLogs stored procedure already exists';
END
GO

-- ================================================
-- 8. Grant Permissions (Optional - uncomment if needed)
-- ================================================
-- IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'AzureAD\Khumeren')
-- BEGIN
--   EXEC sp_addrolemember 'db_owner', 'AzureAD\Khumeren';
--   PRINT '✅ Permissions granted to AzureAD\Khumeren';
-- END
-- GO

-- ================================================
-- Summary
-- ================================================
PRINT '';
PRINT '========================================';
PRINT '🎉 BURGERKING DATABASE SETUP COMPLETE!';
PRINT '========================================';
PRINT '';
PRINT 'Database: SalesBurgerKing';
PRINT 'Tenant: BURGERKING';
PRINT 'Test Sale ID: 11111111-1111-1111-1111-111111111111';
PRINT '';
PRINT '✅ Sales table';
PRINT '✅ SaleItems table';
PRINT '✅ GetSaleWithItems stored procedure';
PRINT '✅ Seed data (1 sale: Whopper, Fries, Coke)';
PRINT '✅ AuditLog table with indexes';
PRINT '✅ PurgeOldAuditLogs procedure';
PRINT '';
PRINT '🚀 Ready for multi-tenant rate limiting tests!';
PRINT '';

-- Quick verification query
SELECT 'BURGERKING Sales' AS TableCheck, COUNT(*) AS RecordCount FROM dbo.Sales;
SELECT 'BURGERKING SaleItems' AS TableCheck, COUNT(*) AS RecordCount FROM dbo.SaleItems;
SELECT 'BURGERKING AuditLog' AS TableCheck, COUNT(*) AS RecordCount FROM dbo.AuditLog;

GO

