using MicroservicesBase.Core.Abstractions;
using MicroservicesBase.Core.Abstractions.Auditing;
using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Core.Configuration;
using MicroservicesBase.Infrastructure.Auditing;
using MicroservicesBase.Infrastructure.Behaviors;
using MicroservicesBase.Infrastructure.Persistence;
using MicroservicesBase.Infrastructure.Queries.Sales;
using MicroservicesBase.Infrastructure.Resilience;
using MicroservicesBase.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using StackExchange.Redis;
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
            // Configure settings
            services.Configure<AuditSettings>(configuration.GetSection(AuditSettings.SectionName));
            services.Configure<ResilienceSettings>(configuration.GetSection(ResilienceSettings.SectionName));

            // ==============================
            // Polly v8 Resilience Pipelines (PLAN ALIGNED)
            // ==============================
            
            // Register static pipelines at startup
            services.AddResiliencePipeline(ResiliencePolicies.MasterDbKey, (builder, context) =>
            {
                var settings = context.ServiceProvider.GetRequiredService<IOptions<ResilienceSettings>>().Value.MasterDb;
                var logger = context.ServiceProvider.GetRequiredService<ILogger<MasterTenantConnectionFactory>>();
                
                builder.AddPipelineFromSettings(
                    settings,
                    ResiliencePolicies.IsSqlTransientError,
                    ResiliencePolicies.MasterDbKey,
                    logger);
            });

            services.AddResiliencePipeline(ResiliencePolicies.RedisKey, (builder, context) =>
            {
                var settings = context.ServiceProvider.GetRequiredService<IOptions<ResilienceSettings>>().Value.Redis;
                var logger = context.ServiceProvider.GetRequiredService<ILogger<CachedTenantConnectionFactoryWithStampedeProtection>>();
                
                builder.AddPipelineFromSettings(
                    settings,
                    ResiliencePolicies.IsRedisTransientError,
                    ResiliencePolicies.RedisKey,
                    logger);
            });
            
            // Register keyed per-tenant pipelines at startup
            services.AddResiliencePipelineRegistry<string>();

            // Register MediatR with audit pipeline behavior
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(GetSaleById.Handler).Assembly);
                
                // Add audit logging pipeline behavior (intercepts all queries/commands)
                cfg.AddOpenBehavior(typeof(AuditLoggingBehavior<,>));
            });

            // Redis distributed cache with lazy singleton (ready for future use: product catalog, sessions)
            // Lazy connection - connects on first use, doesn't block startup
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
            {
                var redisConnectionString = configuration.GetConnectionString("Redis")!;
                var redisConfig = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
                
                // Lazy connect (happens on first use, not at startup)
                // This is the correct pattern - doesn't block app startup if Redis is slow/down
                return StackExchange.Redis.ConnectionMultiplexer.Connect(redisConfig);
            });
            
            // Configure distributed cache (uses lazy multiplexer from DI)
            services.AddStackExchangeRedisCache(options =>
            {
                options.InstanceName = configuration["CacheSettings:InstanceName"] ?? "MicroservicesBase:";
                // ConnectionMultiplexer injected automatically from DI (lazy singleton)
            });

            // Audit logging
            services.AddSingleton<IAuditLogger, AuditLogger>();
            services.AddHostedService(sp => (AuditLogger)sp.GetRequiredService<IAuditLogger>());

            // In-memory cache for tenant connection strings (fast, simple, reliable)
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1000; // Max 1000 cached entries (supports 1000 tenants)
                options.CompactionPercentage = 0.25; // Compact by 25% when limit exceeded
            });

            // Tenant connection factory with in-memory caching (decorator pattern)
            // Inner: MasterTenantConnectionFactory (queries database with Polly resilience)
            // Outer: CachedTenantConnectionFactoryWithStampedeProtection (adds IMemoryCache for speed)
            // Note: Redis (IDistributedCache) still available for future use (product catalog, sessions, etc.)
            services.AddSingleton<MasterTenantConnectionFactory>();
            services.AddSingleton<ITenantConnectionFactory, CachedTenantConnectionFactoryWithStampedeProtection>();

            // register DACs
            services.AddScoped<ISalesReadDac, SalesReadDac>();
            services.AddScoped<ITenantProvider, HeaderTenantProvider>();

            return services;
        }
    }
}