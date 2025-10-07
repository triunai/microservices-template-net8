using MicroservicesBase.Core.Abstractions;
using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Infrastructure.Persistence;
using MicroservicesBase.Infrastructure.Queries.Sales;
using MicroservicesBase.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // register MediatR (scans this assembly for handlers)
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(GetSaleById.Handler).Assembly));

            // Redis distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = configuration["CacheSettings:InstanceName"] ?? "MicroservicesBase:";
            });

            // Tenant connection factory with Redis caching + stampede protection (decorator pattern)
            // Inner: MasterTenantConnectionFactory (queries database)
            // Outer: CachedTenantConnectionFactoryWithStampedeProtection (adds Redis caching + concurrency control)
            services.AddSingleton<MasterTenantConnectionFactory>();
            services.AddSingleton<ITenantConnectionFactory, CachedTenantConnectionFactoryWithStampedeProtection>();

            // register DACs
            services.AddScoped<ISalesReadDac, SalesReadDac>();
            services.AddScoped<ITenantProvider, HeaderTenantProvider>();

            return services;
        }
    }
}