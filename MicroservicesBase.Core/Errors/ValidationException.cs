namespace MicroservicesBase.Core.Errors
{
    /// <summary>
    /// Exception thrown when validation fails at the domain level.
    /// Maps to HTTP 400 Bad Request.
    /// Note: This is different from FluentValidation's input validation.
    /// Use this for domain/business validation that can't be caught at the DTO level.
    /// </summary>
    public sealed class ValidationException : AppException
    {
        public Dictionary<string, string[]> Errors { get; }
        
        public ValidationException(string message, Dictionary<string, string[]> errors) 
            : base(ErrorCatalog.VALIDATION_ERROR, message)
        {
            Errors = errors;
        }
        
        public ValidationException(string message) 
            : base(ErrorCatalog.VALIDATION_ERROR, message)
        {
            Errors = new Dictionary<string, string[]>();
        }
        
        public static ValidationException SingleField(string field, string error)
        {
            var errors = new Dictionary<string, string[]>
            {
                [field] = new[] { error }
            };
            return new ValidationException("Validation failed.", errors);
        }
    }
}

