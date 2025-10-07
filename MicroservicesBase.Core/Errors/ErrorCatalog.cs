namespace MicroservicesBase.Core.Errors
{
    /// <summary>
    /// Centralized catalog of all application error codes.
    /// These codes are returned in ProblemDetails responses and used for client-side error handling.
    /// Format: DOMAIN_ERROR_TYPE (e.g., SALE_NOT_FOUND, TENANT_INACTIVE)
    /// </summary>
    public static class ErrorCatalog
    {
        // ===== General Errors =====
        public const string INTERNAL_ERROR = "INTERNAL_ERROR";
        public const string VALIDATION_ERROR = "VALIDATION_ERROR";
        public const string UNAUTHORIZED = "UNAUTHORIZED";
        public const string FORBIDDEN = "FORBIDDEN";
        
        // ===== Tenant Errors =====
        public const string TENANT_NOT_FOUND = "TENANT_NOT_FOUND";
        public const string TENANT_INACTIVE = "TENANT_INACTIVE";
        public const string TENANT_CONFLICT = "TENANT_CONFLICT";
        public const string TENANT_HEADER_MISSING = "TENANT_HEADER_MISSING";
        
        // ===== Sale Errors =====
        public const string SALE_NOT_FOUND = "SALE_NOT_FOUND";
        public const string SALE_ALREADY_VOIDED = "SALE_ALREADY_VOIDED";
        public const string SALE_DUPLICATE_RECEIPT = "SALE_DUPLICATE_RECEIPT";
        public const string SALE_INVALID_TOTAL = "SALE_INVALID_TOTAL";
        
        // ===== Item Errors =====
        public const string ITEM_NOT_FOUND = "ITEM_NOT_FOUND";
        public const string ITEM_INSUFFICIENT_STOCK = "ITEM_INSUFFICIENT_STOCK";
        public const string ITEM_INVALID_QUANTITY = "ITEM_INVALID_QUANTITY";
        
        // ===== Payment Errors =====
        public const string PAYMENT_FAILED = "PAYMENT_FAILED";
        public const string PAYMENT_DECLINED = "PAYMENT_DECLINED";
        public const string PAYMENT_INVALID_AMOUNT = "PAYMENT_INVALID_AMOUNT";
        
        /// <summary>
        /// Maps error codes to HTTP status codes.
        /// This provides a single source of truth for status code mapping.
        /// </summary>
        public static int GetStatusCode(string errorCode) => errorCode switch
        {
            // 400 Bad Request
            VALIDATION_ERROR => 400,
            SALE_INVALID_TOTAL => 400,
            ITEM_INVALID_QUANTITY => 400,
            PAYMENT_INVALID_AMOUNT => 400,
            TENANT_HEADER_MISSING => 400,
            
            // 401 Unauthorized
            UNAUTHORIZED => 401,
            
            // 403 Forbidden
            FORBIDDEN => 403,
            TENANT_INACTIVE => 403,
            
            // 404 Not Found
            TENANT_NOT_FOUND => 404,
            SALE_NOT_FOUND => 404,
            ITEM_NOT_FOUND => 404,
            
            // 409 Conflict
            TENANT_CONFLICT => 409,
            SALE_DUPLICATE_RECEIPT => 409,
            SALE_ALREADY_VOIDED => 409,
            
            // 402 Payment Required (or 400 for payment issues)
            PAYMENT_FAILED => 402,
            PAYMENT_DECLINED => 402,
            
            // 422 Unprocessable Entity
            ITEM_INSUFFICIENT_STOCK => 422,
            
            // 500 Internal Server Error (default)
            _ => 500
        };
        
        /// <summary>
        /// Maps error codes to user-friendly titles.
        /// </summary>
        public static string GetTitle(string errorCode) => errorCode switch
        {
            // General
            INTERNAL_ERROR => "Internal Server Error",
            VALIDATION_ERROR => "Validation Failed",
            UNAUTHORIZED => "Unauthorized",
            FORBIDDEN => "Forbidden",
            
            // Tenant
            TENANT_NOT_FOUND => "Tenant Not Found",
            TENANT_INACTIVE => "Tenant Inactive",
            TENANT_CONFLICT => "Tenant Conflict",
            TENANT_HEADER_MISSING => "Tenant Header Missing",
            
            // Sale
            SALE_NOT_FOUND => "Sale Not Found",
            SALE_ALREADY_VOIDED => "Sale Already Voided",
            SALE_DUPLICATE_RECEIPT => "Duplicate Receipt Number",
            SALE_INVALID_TOTAL => "Invalid Sale Total",
            
            // Item
            ITEM_NOT_FOUND => "Item Not Found",
            ITEM_INSUFFICIENT_STOCK => "Insufficient Stock",
            ITEM_INVALID_QUANTITY => "Invalid Quantity",
            
            // Payment
            PAYMENT_FAILED => "Payment Failed",
            PAYMENT_DECLINED => "Payment Declined",
            PAYMENT_INVALID_AMOUNT => "Invalid Payment Amount",
            
            _ => "An Error Occurred"
        };
    }
}

