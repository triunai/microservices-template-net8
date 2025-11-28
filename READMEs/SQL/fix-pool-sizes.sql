-- ================================================
-- FIX: Add Max Pool Size to Tenant Connection Strings
-- ================================================
-- This fixes the connection pool exhaustion under load
-- Default pool size is 100, which causes deadlocks with 100+ concurrent requests
-- ================================================

USE TenantMaster;
GO

-- Check current connection strings
SELECT Name, ConnectionString FROM dbo.Tenants;
GO

-- Update 7ELEVEN with Max Pool Size=200
UPDATE dbo.Tenants
SET ConnectionString = N'Server=RGT-KHUMEREN;Database=Sales7Eleven;Trusted_Connection=True;TrustServerCertificate=True;Max Pool Size=200;Min Pool Size=10;'
WHERE Name = '7ELEVEN';

-- Update BURGERKING with Max Pool Size=200
UPDATE dbo.Tenants
SET ConnectionString = N'Server=RGT-KHUMEREN;Database=SalesBurgerKing;Trusted_Connection=True;TrustServerCertificate=True;Max Pool Size=200;Min Pool Size=10;'
WHERE Name = 'BURGERKING';

-- Verify the fix
SELECT Name, ConnectionString FROM dbo.Tenants;
GO

PRINT '';
PRINT '========================================';
PRINT '✅ POOL SIZE FIX APPLIED!';
PRINT '========================================';
PRINT '';
PRINT 'Both tenants now have:';
PRINT '  - Max Pool Size: 200 (was 100 default)';
PRINT '  - Min Pool Size: 10  (was 0 default)';
PRINT '';
PRINT '🚀 Restart API and rerun k6 test!';
PRINT '';

