using Rgt.Space.Core.Abstractions.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Rgt.Space.Infrastructure.Tenancy
{
    public sealed class MasterTenantConnectionFactory : ITenantConnectionFactory
    {
        private readonly string _portalDbConnection;
        private readonly ILogger<MasterTenantConnectionFactory> _logger;

        public MasterTenantConnectionFactory(
            IConfiguration config,
            ILogger<MasterTenantConnectionFactory> logger)
        {
            // Single-DB Architecture: We use one database for everything.
            // We fall back to "TenantMaster" if "PortalDb" is missing, for backward compatibility during migration.
            _portalDbConnection = config.GetConnectionString("PortalDb") 
                               ?? config.GetConnectionString("TenantMaster")!;
            
            _logger = logger;
            
            _logger.LogInformation("MasterTenantConnectionFactory initialized in Single-DB mode.");
        }

        public Task<string> GetSqlConnectionStringAsync(string tenantId, CancellationToken ct = default)
        {
            // In Single-DB mode, we ignore the tenantId for connection resolution 
            // because all tenants live in the same database.
            // Data isolation is handled via 'organization_id' or logical separation in the queries.
            
            return Task.FromResult(_portalDbConnection);
        }
    }
}
