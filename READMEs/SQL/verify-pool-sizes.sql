-- ================================================
-- VERIFY: Check if Pool Sizes Were Applied
-- ================================================
USE TenantMaster;
GO

PRINT '';
PRINT '========================================';
PRINT '🔍 CURRENT CONNECTION STRINGS';
PRINT '========================================';
PRINT '';

SELECT 
    Name AS TenantName,
    ConnectionString,
    CASE 
        WHEN ConnectionString LIKE '%Max Pool Size%' THEN '✅ HAS POOL SIZE'
        ELSE '❌ MISSING POOL SIZE (USING DEFAULT 100!)'
    END AS PoolSizeStatus,
    IsActive
FROM dbo.Tenants
ORDER BY Name;

PRINT '';
PRINT '========================================';
PRINT '🎯 WHAT TO LOOK FOR:';
PRINT '========================================';
PRINT '✅ Both tenants should show "Max Pool Size=200"';
PRINT '❌ If missing, run SQL/fix-pool-sizes.sql';
PRINT '';

