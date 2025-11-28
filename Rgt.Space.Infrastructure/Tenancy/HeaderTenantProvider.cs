using Rgt.Space.Core.Abstractions.Tenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rgt.Space.Infrastructure.Tenancy
{
    public sealed class HeaderTenantProvider : ITenantProvider
    {
        public string? Id { get; private set; }

        public void SetTenant(string tenant) => Id = tenant;
    }

}
