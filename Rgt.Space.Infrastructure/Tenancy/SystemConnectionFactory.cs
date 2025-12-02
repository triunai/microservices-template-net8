using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Tenancy;

namespace Rgt.Space.Infrastructure.Tenancy;

public class SystemConnectionFactory : ISystemConnectionFactory
{
    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SystemConnectionFactory> _logger;

    public SystemConnectionFactory(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<SystemConnectionFactory> logger)
    {
        // Direct read from configuration. No "Tenant" concept involved.
        // We look for "PortalDb" or "DefaultConnection" or "TenantMaster" as fallback.
        _connectionString = configuration.GetConnectionString("PortalDb") 
                            ?? configuration.GetConnectionString("DefaultConnection")
                            ?? configuration.GetConnectionString("TenantMaster")
                            ?? throw new InvalidOperationException("No system database connection string found (PortalDb/DefaultConnection/TenantMaster).");
        
        _cache = cache;
        _logger = logger;
    }

    public Task<string> GetConnectionStringAsync(CancellationToken ct = default)
    {
        // Since the connection string is static from config, we don't strictly need caching or stampede protection here.
        // However, if we ever move to fetching this from a vault or remote config, the pattern is ready.
        // For now, we just return the static string.
        return Task.FromResult(_connectionString);
    }
}
