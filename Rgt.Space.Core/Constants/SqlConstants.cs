namespace Rgt.Space.Core.Constants;

/// <summary>
/// SQL Server-related constants (error codes, queries, timeouts).
/// Centralizes all SQL magic values for transient error handling and query management.
/// </summary>
public static class SqlConstants
{
    /// <summary>
    /// SQL Server error codes for transient errors (should be retried by Polly).
    /// Reference: https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// Serialization failure (deadlock/concurrency)
        /// </summary>
        public const string SerializationFailure = "40001";
        
        /// <summary>
        /// Deadlock detected
        /// </summary>
        public const string DeadlockDetected = "40P01";
        
        /// <summary>
        /// Connection exception (generic)
        /// </summary>
        public const string ConnectionException = "08000";
        
        /// <summary>
        /// Connection does not exist
        /// </summary>
        public const string ConnectionDoesNotExist = "08003";
        
        /// <summary>
        /// Connection failure
        /// </summary>
        public const string ConnectionFailure = "08006";
        
        /// <summary>
        /// Cannot connect now (system starting up)
        /// </summary>
        public const string CannotConnectNow = "57P03";
        
        /// <summary>
        /// Admin shutdown
        /// </summary>
        public const string AdminShutdown = "57P01";
        
        /// <summary>
        /// Crash shutdown
        /// </summary>
        public const string CrashShutdown = "57P02";
    }

    /// <summary>
    /// Common SQL queries (non-stored procedure queries)
    /// </summary>
    public static class Queries
    {
        /// <summary>
        /// Get tenant connection string from master database
        /// </summary>
        public const string GetTenantConnectionString = 
            "SELECT connection_string FROM tenants WHERE code = @tenant_code AND status = 'Active'";
        
        /// <summary>
        /// Check if tenant exists and is active
        /// </summary>
        public const string CheckTenantActive = 
            "SELECT 1 FROM tenants WHERE code = @tenant_code AND status = 'Active'";
    }

    /// <summary>
    /// SQL command timeout defaults (in seconds)
    /// Should be LESS than Polly's outer timeout to ensure Polly is the decider
    /// </summary>
    public static class CommandTimeouts
    {
        public const int MasterDb = 1;      // 1s (Polly timeout: 700ms, but gives buffer for cancellation)
        public const int TenantDb = 1;      // 1s (Polly timeout: 1500ms)
        public const int AuditDb = 3;       // 3s (Polly timeout: 4000ms)
        public const int Default = 30;      // SQL Server default
    }
}

