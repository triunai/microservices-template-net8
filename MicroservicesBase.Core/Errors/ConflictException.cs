namespace MicroservicesBase.Core.Errors
{
    /// <summary>
    /// Exception thrown when a conflict is detected (duplicate, already exists, etc.).
    /// Maps to HTTP 409 Conflict.
    /// </summary>
    public sealed class ConflictException : AppException
    {
        public ConflictException(string errorCode, string message) 
            : base(errorCode, message)
        {
        }
        
        public static ConflictException DuplicateReceipt(string receiptNumber) 
            => new(ErrorCatalog.SALE_DUPLICATE_RECEIPT, 
                $"A sale with receipt number '{receiptNumber}' already exists.");
        
        public static ConflictException SaleAlreadyVoided(Guid saleId) 
            => new(ErrorCatalog.SALE_ALREADY_VOIDED, 
                $"Sale '{saleId}' has already been voided and cannot be modified.");
    }
}

