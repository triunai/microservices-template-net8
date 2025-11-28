using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Abstractions.Tenancy
{
    // Provides the current tenant context (resolved from middleware / JWT / header).

    public interface ITenantProvider
    {
        string? Id { get; }
    }
}
