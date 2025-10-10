using Dapper;
using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Core.Constants;
using MicroservicesBase.Core.Errors;
using MicroservicesBase.Infrastructure.Resilience;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Infrastructure.Tenancy
{
    public sealed class MasterTenantConnectionFactory : ITenantConnectionFactory
    {
        private readonly string _masterConn;
        private readonly ResiliencePipeline _masterDbPipeline;
        private readonly ILogger<MasterTenantConnectionFactory> _logger;

        public MasterTenantConnectionFactory(
            IConfiguration config,
            ResiliencePipelineProvider<string> pipelineProvider,
            ILogger<MasterTenantConnectionFactory> logger)
        {
            // Connection string to Master DB from appsettings.json
            _masterConn = config.GetConnectionString("TenantMaster")!;
            _masterDbPipeline = pipelineProvider.GetPipeline(ResiliencePolicies.MasterDbKey);
            _logger = logger;
            
            _logger.LogInformation("MasterTenantConnectionFactory initialized with connection string: {MasterConn}", 
                _masterConn.Substring(0, Math.Min(_masterConn.Length, 50)) + "...");
        }

        public async Task<string> GetSqlConnectionStringAsync(string tenantId, CancellationToken ct = default)
        {
            _logger.LogDebug("Resolving tenant connection string for: {TenantId}", tenantId);
            
            return await _masterDbPipeline.ExecuteAsync(async token =>
            {
                await using var conn = new SqlConnection(_masterConn);
                await conn.OpenAsync(token); // Propagate cancellation token
                
                // Set CommandTimeout to 1s (less than Polly's 700ms outer timeout)
                // This ensures Polly handles the timeout, not ADO.NET
                var cmd = new SqlCommand(SqlConstants.Queries.GetTenantConnectionString, conn)
                {
                    CommandTimeout = SqlConstants.CommandTimeouts.MasterDb
                };
                cmd.Parameters.AddWithValue("@TenantId", tenantId);
                
                var cs = await cmd.ExecuteScalarAsync(token) as string;
                
                if (cs is null)
                {
                    _logger.LogWarning("Tenant not found or inactive: {TenantId}", tenantId);
                    throw TenantException.NotFound(tenantId);
                }

                _logger.LogDebug("Successfully resolved connection string for tenant: {TenantId}", tenantId);
                return cs;
            }, ct);
        }
    }
}
