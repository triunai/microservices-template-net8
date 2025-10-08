using MicroservicesBase.Core.Abstractions;
using MicroservicesBase.Core.Abstractions.Auditing;
using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Core.Configuration;
using MicroservicesBase.Infrastructure.Auditing;
using MicroservicesBase.Infrastructure.Behaviors;
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
            // Configure audit settings
            services.Configure<AuditSettings>(configuration.GetSection(AuditSettings.SectionName));

            // Register MediatR with audit pipeline behavior
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(GetSaleById.Handler).Assembly);
                
                // Add audit logging pipeline behavior (intercepts all queries/commands)
                cfg.AddOpenBehavior(typeof(AuditLoggingBehavior<,>));
            });

            // Redis distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = configuration["CacheSettings:InstanceName"] ?? "MicroservicesBase:";
            });

            // Audit logging
            services.AddSingleton<IAuditLogger, AuditLogger>();
            services.AddHostedService(sp => (AuditLogger)sp.GetRequiredService<IAuditLogger>());

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