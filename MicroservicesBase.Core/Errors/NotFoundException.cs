namespace MicroservicesBase.Core.Errors
{
    /// <summary>
    /// Exception thrown when a requested resource is not found.
    /// Maps to HTTP 404 Not Found.
    /// </summary>
    public sealed class NotFoundException : AppException
    {
        public NotFoundException(string errorCode, string message) 
            : base(errorCode, message)
        {
        }
        
        public static NotFoundException Sale(Guid saleId) 
            => new(ErrorCatalog.SALE_NOT_FOUND, $"Sale with ID '{saleId}' was not found.");
        
        public static NotFoundException Tenant(string tenantId) 
            => new(ErrorCatalog.TENANT_NOT_FOUND, $"Tenant '{tenantId}' was not found.");
        
        public static NotFoundException Item(string sku) 
            => new(ErrorCatalog.ITEM_NOT_FOUND, $"Item with SKU '{sku}' was not found.");
    }
}

