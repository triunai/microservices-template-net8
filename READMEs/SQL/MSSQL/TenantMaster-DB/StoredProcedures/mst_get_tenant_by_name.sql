CREATE PROCEDURE [dbo].[mst_get_tenant_by_name]
    @TenantKey NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        [Name],
        [IsActive]
    FROM [dbo].[Tenants]
    WHERE [Name] = @TenantKey;
END
GO
