namespace MicroservicesBase.Core.Errors
{
    /// <summary>
    /// Base exception for all application-specific exceptions.
    /// Includes an error code from ErrorCatalog for structured error handling.
    /// </summary>
    public abstract class AppException : Exception
    {
        public string ErrorCode { get; }
        
        protected AppException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
        
        protected AppException(string errorCode, string message, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}

