namespace MicroservicesBase.Core.Constants;

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
        /// Connection timeout (network or server unreachable)
        /// </summary>
        public const int Timeout = -2;
        
        /// <summary>
        /// Connection broken or refused
        /// </summary>
        public const int ConnectionBroken = -1;
        
        /// <summary>
        /// Deadlock victim (transaction was chosen as deadlock victim and rolled back)
        /// </summary>
        public const int Deadlock = 1205;
        
        /// <summary>
        /// Transport-level error on send
        /// </summary>
        public const int TransportError = 64;
        
        /// <summary>
        /// Timeout waiting for lock
        /// </summary>
        public const int LockTimeout = 1222;
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
            "SELECT ConnectionString FROM Tenants WHERE Name = @TenantId AND IsActive = 1";
        
        /// <summary>
        /// Check if tenant exists and is active
        /// </summary>
        public const string CheckTenantActive = 
            "SELECT 1 FROM Tenants WHERE Name = @TenantId AND IsActive = 1";
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

