using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Abstractions.Tenancy
{
    public interface ITenantConnectionFactory
    {
        Task<string> GetSqlConnectionStringAsync(string tenantId, CancellationToken ct = default);
    }
}
