using MicroservicesBase.Core.Abstractions.Tenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Infrastructure.Tenancy
{
    public sealed class HeaderTenantProvider : ITenantProvider
    {
        public string? Id { get; private set; }

        public void SetTenant(string tenant) => Id = tenant;
    }

}
