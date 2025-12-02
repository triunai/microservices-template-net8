namespace Rgt.Space.Core.Errors
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
        public const string SALE_ID_INVALID = "SALE_ID_INVALID";
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
        
        // ===== Portal Routing Errors =====
        public const string ROUTING_URL_ALREADY_EXISTS = "ROUTING_URL_ALREADY_EXISTS";
        public const string PROJECT_NOT_FOUND = "PROJECT_NOT_FOUND";
        public const string INVALID_POSITION_CODE = "INVALID_POSITION_CODE";
        public const string ASSIGNMENT_NOT_FOUND = "ASSIGNMENT_NOT_FOUND";
        
        /// <summary>
        /// Determines if an error code represents a validation error.
        /// Validation errors return 400 Bad Request.
        /// 
        /// Uses a layered approach:
        /// 1. Explicit VALIDATION_ERROR constant
        /// 2. Pattern-based detection for custom error codes (*_INVALID, *_REQUIRED, etc.)
        /// 3. Safety net for FluentValidation default error codes (*Validator)
        /// </summary>
        public static bool IsValidationError(string errorCode)
        {
            // Layer 1: Explicit validation marker
            if (errorCode == VALIDATION_ERROR)
                return true;
            
            // Layer 2: Custom validation patterns (your error code conventions)
            if (errorCode.EndsWith("_INVALID") || 
                errorCode.EndsWith("_REQUIRED") ||
                errorCode.EndsWith("_FORMAT_INVALID") ||
                errorCode.EndsWith("_TOO_LONG") ||
                errorCode.EndsWith("_TOO_SHORT") ||
                errorCode.EndsWith("_CODE")) // Catches INVALID_POSITION_CODE
                return true;
            
            // Layer 3: SAFETY NET - Catch FluentValidation default error codes
            // Handles cases where .WithErrorCode() is forgotten
            // Examples: NotEqualValidator, NotEmptyValidator, LengthValidator, etc.
            if (errorCode.EndsWith("Validator"))
                return true;
            
            // Layer 3b: Additional FluentValidation patterns
            if (errorCode.StartsWith("NotEmpty") || 
                errorCode.StartsWith("NotEqual") ||
                errorCode.StartsWith("GreaterThan") ||
                errorCode.StartsWith("LessThan") ||
                errorCode.StartsWith("Must"))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Maps error codes to HTTP status codes.
        /// This provides a single source of truth for status code mapping.
        /// </summary>
        public static int GetStatusCode(string errorCode)
        {
            // Early return for validation errors (fast path - handles 90% of bad requests)
            // This avoids checking the entire switch for validation errors
            if (IsValidationError(errorCode))
                return 400;
            
            // Business logic errors only (remaining 10%)
            return errorCode switch
            {
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
                
                // 402 Payment Required
                PAYMENT_FAILED => 402,
                PAYMENT_DECLINED => 402,
                
                // 422 Unprocessable Entity
                ITEM_INSUFFICIENT_STOCK => 422,
                
                // Portal Routing
                ROUTING_URL_ALREADY_EXISTS => 409,
                PROJECT_NOT_FOUND => 404,
                ASSIGNMENT_NOT_FOUND => 404,

                // 500 Internal Server Error (default)
                _ => 500
            };
        }
        
        /// <summary>
        /// Maps error codes to user-friendly titles.
        /// Uses early return for validation errors (consistency with GetStatusCode).
        /// </summary>
        public static string GetTitle(string errorCode)
        {
            // Early return for validation errors (consistent messaging)
            if (IsValidationError(errorCode))
                return "Validation Failed";
            
            // Business logic error titles
            return errorCode switch
            {
                // General
                INTERNAL_ERROR => "Internal Server Error",
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
                
                // Item
                ITEM_NOT_FOUND => "Item Not Found",
                ITEM_INSUFFICIENT_STOCK => "Insufficient Stock",
                
                // Payment
                PAYMENT_FAILED => "Payment Failed",
                PAYMENT_DECLINED => "Payment Declined",
                
                // Portal Routing
                ROUTING_URL_ALREADY_EXISTS => "Routing URL Already Exists",
                PROJECT_NOT_FOUND => "Project Not Found",
                ASSIGNMENT_NOT_FOUND => "Assignment Not Found",

                _ => "An Error Occurred"
            };
        }
    }
}

