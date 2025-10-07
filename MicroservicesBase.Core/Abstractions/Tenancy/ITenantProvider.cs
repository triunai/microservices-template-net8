using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Core.Abstractions.Tenancy
{
    // Provides the current tenant context (resolved from middleware / JWT / header).

    public interface ITenantProvider
    {
        string? Id { get; }
    }
}
