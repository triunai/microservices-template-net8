using System.Data;

namespace Rgt.Space.Core.Abstractions.Tenancy;

public interface ISystemConnectionFactory
{
    Task<string> GetConnectionStringAsync(CancellationToken ct = default);
}
