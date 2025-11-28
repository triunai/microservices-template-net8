namespace Rgt.Space.Core.Errors
{
    /// <summary>
    /// Exception thrown for tenant-related errors (inactive, conflict, missing).
    /// </summary>
    public sealed class TenantException : AppException
    {
        public TenantException(string errorCode, string message) 
            : base(errorCode, message)
        {
        }
        
        public static TenantException NotFound(string tenantId) 
            => new(ErrorCatalog.TENANT_NOT_FOUND, $"Tenant '{tenantId}' does not exist.");
        
        public static TenantException Inactive(string tenantId) 
            => new(ErrorCatalog.TENANT_INACTIVE, $"Tenant '{tenantId}' is inactive and cannot process requests.");
        
        public static TenantException Conflict(string headerTenant, string jwtTenant) 
            => new(ErrorCatalog.TENANT_CONFLICT, 
                $"Tenant mismatch: X-Tenant header '{headerTenant}' does not match JWT tenant '{jwtTenant}'.");
        
        public static TenantException HeaderMissing() 
            => new(ErrorCatalog.TENANT_HEADER_MISSING, "X-Tenant header is required but was not provided.");
    }
}

