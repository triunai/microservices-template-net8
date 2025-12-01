-- Master DB
IF DB_ID('TenantMaster') IS NULL CREATE DATABASE TenantMaster;
GO
USE TenantMaster;
GO

IF OBJECT_ID('dbo.Tenants') IS NULL
BEGIN
  CREATE TABLE dbo.Tenants(
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    ConnectionString NVARCHAR(500) NOT NULL,
    IsActive BIT NOT NULL DEFAULT(1)
  );
END
GO

-- Tenant DBs on the SAME LocalDB instance
IF DB_ID('Sales7Eleven') IS NULL CREATE DATABASE Sales7Eleven;
IF DB_ID('SalesBurgerKing') IS NULL CREATE DATABASE SalesBurgerKing;
GO

-- Seed tenant connection strings (pointing to LocalDB)
MERGE dbo.Tenants AS t
USING (VALUES
 ('7ELEVEN',    N'Server=(localdb)\MSSQLLocalDB;Database=Sales7Eleven;Integrated Security=True;TrustServerCertificate=True;'),
 ('BURGERKING', N'Server=(localdb)\MSSQLLocalDB;Database=SalesBurgerKing;Integrated Security=True;TrustServerCertificate=True;')
) s(Name, ConnectionString)
ON t.Name = s.Name
WHEN MATCHED THEN UPDATE SET ConnectionString = s.ConnectionString, IsActive = 1
WHEN NOT MATCHED THEN INSERT (Name, ConnectionString, IsActive) VALUES (s.Name, s.ConnectionString, 1);
GO




USE Sales7Eleven;
GO

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
END
GO

IF OBJECT_ID('dbo.SaleItems') IS NULL
BEGIN
  CREATE TABLE dbo.SaleItems (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    SaleId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.Sales(Id),
    Sku NVARCHAR(50) NOT NULL,
    Qty INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL
  );
END
GO

IF OBJECT_ID('dbo.GetSaleWithItems') IS NULL
EXEC('
CREATE PROCEDURE dbo.GetSaleWithItems @SaleId UNIQUEIDENTIFIER AS
BEGIN
  SET NOCOUNT ON;
  SELECT Id,TenantId,StoreId,RegisterId,ReceiptNumber,CreatedAt,NetTotal,TaxTotal,GrandTotal
  FROM dbo.Sales WITH (NOLOCK) WHERE Id = @SaleId;

  SELECT Sku,Qty,UnitPrice
  FROM dbo.SaleItems WITH (NOLOCK) WHERE SaleId = @SaleId;
END
');
GO

DECLARE @SaleId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
IF NOT EXISTS (SELECT 1 FROM dbo.Sales WHERE Id=@SaleId)
BEGIN
  INSERT dbo.Sales(Id,TenantId,StoreId,RegisterId,ReceiptNumber,CreatedAt,NetTotal,TaxTotal,GrandTotal)
  VALUES (@SaleId,'7ELEVEN','STORE001','REG01','RCP-7E-001',SYSDATETIMEOFFSET(),20.00,1.20,21.20);

  INSERT dbo.SaleItems(SaleId,Sku,Qty,UnitPrice)
  VALUES (@SaleId,'SKU-COFFEE',1,10.00),
         (@SaleId,'SKU-SANDWICH',1,10.00);
END
GO




USE TenantMaster;
GO

SELECT Name, ConnectionString FROM dbo.Tenants; -- see what's there

-- update the tenants to use your machine instance
UPDATE dbo.Tenants
SET ConnectionString = N'Server=RGT-KHUMEREN;Database=Sales7Eleven;Trusted_Connection=True;TrustServerCertificate=True;'
WHERE Name = '7ELEVEN';

UPDATE dbo.Tenants
SET ConnectionString = N'Server=RGT-KHUMEREN;Database=SalesBurgerKing;Trusted_Connection=True;TrustServerCertificate=True;'
WHERE Name = 'BURGERKING';


USE Sales7Eleven;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'AzureAD\Khumeren')
    CREATE USER [AzureAD\Khumeren] FOR LOGIN [AzureAD\Khumeren];
EXEC sp_addrolemember 'db_owner', 'AzureAD\Khumeren';

