using Dapper;
using MicroservicesBase.Core.Abstractions.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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

        public MasterTenantConnectionFactory(IConfiguration config)
        {
            // Connection string to Master DB from appsettings.json
            _masterConn = config.GetConnectionString("TenantMaster")!;
            Console.WriteLine($"[MASTER CS] {_masterConn}");
        }

        public async Task<string> GetSqlConnectionStringAsync(string tenantId, CancellationToken ct = default)
        {
            Console.WriteLine($"[RESOLVING TENANT] {tenantId}");
            
            await using var conn = new SqlConnection(_masterConn);
            await conn.OpenAsync(ct); // ← THIS WAS MISSING!
            
            var sql = "SELECT ConnectionString FROM Tenants WHERE Name = @TenantId";
            var cs = await conn.QueryFirstOrDefaultAsync<string>(sql, new { TenantId = tenantId });
            
            if (cs is null)
                throw new InvalidOperationException($"No tenant found with name '{tenantId}'");

            Console.WriteLine($"[TENANT CS] {tenantId} => {cs}");

            return cs;
        }
    }
}
