using Rgt.Space.Core.Abstractions;
using Rgt.Space.Core.Abstractions.Auditing;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Infrastructure.Auditing;
using Rgt.Space.Infrastructure.Behaviors;
using Rgt.Space.Infrastructure.Queries.Sales;
using Rgt.Space.Infrastructure.Resilience;
using Rgt.Space.Infrastructure.Tenancy;
using Rgt.Space.Infrastructure.Mapping.Audit;
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
using Rgt.Space.Infrastructure.Persistence.Services.Audit;
using Rgt.Space.Infrastructure.Persistence.Services.Identity;
using Rgt.Space.Infrastructure.Persistence.Dac.Identity;
using Rgt.Space.Infrastructure.Persistence.Dac;
using Rgt.Space.Infrastructure.Identity;

namespace Rgt.Space.Infrastructure
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

            // Register PortalDb pipeline (single-database for Portal Routing)
            services.AddResiliencePipeline("PortalDb", (builder, context) =>
            {
                var settings = context.ServiceProvider.GetRequiredService<IOptions<ResilienceSettings>>().Value.TenantDb;
                var logger = context.ServiceProvider.GetRequiredService<ILogger<Persistence.Dac.PortalRouting.ClientProjectMappingWriteDac>>();

                builder.AddPipelineFromSettings(
                    settings,
                    ResiliencePolicies.IsSqlTransientError,
                    "PortalDb",
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
            services.AddSingleton<ISystemConnectionFactory, SystemConnectionFactory>();

            // register DACs
            services.AddScoped<ISalesReadDac, SalesReadDac>();
            services.AddScoped<ITenantProvider, HeaderTenantProvider>();

            // Register Identity DACs
            services.AddScoped<Core.Abstractions.Identity.IUserReadDac, UserReadDac>();
            services.AddScoped<Core.Abstractions.Identity.IUserWriteDac, UserWriteDac>();
            
            // Register Identity Services
            services.AddScoped<Core.Abstractions.Identity.IIdentitySyncService, IdentitySyncService>();

            // Current User Context
            // TODO: Make this configurable via appsettings (e.g., Auth:EnableMockAuth)
            // For now, we default to DevCurrentUser to unblock development
            services.AddScoped<Core.Abstractions.Identity.ICurrentUser, CurrentUser>(); 
            // services.AddScoped<Core.Abstractions.Identity.ICurrentUser, DevCurrentUser>();
            
            // Register Portal Routing DACs
            services.AddScoped<Core.Abstractions.PortalRouting.IClientReadDac, Persistence.Dac.PortalRouting.ClientReadDac>();
            services.AddScoped<Core.Abstractions.PortalRouting.IProjectReadDac, Persistence.Dac.PortalRouting.ProjectReadDac>();
            services.AddScoped<Core.Abstractions.PortalRouting.IClientProjectMappingReadDac, Persistence.Dac.PortalRouting.ClientProjectMappingReadDac>();
            services.AddScoped<Core.Abstractions.PortalRouting.IClientProjectMappingWriteDac, Persistence.Dac.PortalRouting.ClientProjectMappingWriteDac>();
            
            // Register Task Allocation DACs
            services.AddScoped<Core.Abstractions.TaskAllocation.IProjectAssignmentReadDac, Persistence.Dac.TaskAllocation.ProjectAssignmentReadDac>();
            services.AddScoped<Core.Abstractions.TaskAllocation.ITaskAllocationWriteDac, Persistence.Dac.TaskAllocation.TaskAllocationWriteDac>();

            // Register Dashboard DACs
            services.AddScoped<Core.Abstractions.Dashboard.IDashboardReadDac, Persistence.Dac.Dashboard.DashboardReadDac>();

            // Register Mapperly mappers (singleton - stateless, compile-time generated)
            // Zero runtime overhead, no reflection, just pure generated C# code
            services.AddSingleton<Mapping.SalesMapper>();
            services.AddSingleton<Mapping.Audit.AuditPayloadMapper>();
            services.AddSingleton<Mapping.PortalRoutingMapper>();
            services.AddSingleton<Mapping.TaskAllocationMapper>();

            // Register audit services
            services.AddScoped<IAuditPayloadDecoderService, AuditPayloadDecoderService>();

            return services;
        }
    }
}