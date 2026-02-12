using Rgt.Space.Core.Abstractions;
using Rgt.Space.Core.Abstractions.Auditing;
using Rgt.Space.Core.Abstractions.Debugging;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Core.Debugging;
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
using Microsoft.Extensions.Hosting;
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

            // Register AuditDb pipeline (single-database for Audit Logs)
            services.AddResiliencePipeline("AuditDb", (builder, context) =>
            {
                // Usage: Write-heavy, non-critical audit logs
                // Config: Uses AuditDb settings
                var settings = context.ServiceProvider.GetRequiredService<IOptions<ResilienceSettings>>().Value.AuditDb;
                var logger = context.ServiceProvider.GetRequiredService<ILogger<AuditLogger>>();

                builder.AddPipelineFromSettings(
                    settings,
                    ResiliencePolicies.IsSqlTransientError,
                    "AuditDb",
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

            // Register MediatR with pipeline behaviors
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(GetSaleById.Handler).Assembly);
                
                // Pipeline behavior order matters!
                // 1. Checkpoint tracking (must be first to track handler entry before audit)
                cfg.AddOpenBehavior(typeof(CheckpointPipelineBehavior<,>));
                
                // 2. Audit logging (intercepts all queries/commands, runs after checkpoint tracking)
                cfg.AddOpenBehavior(typeof(AuditLoggingBehavior<,>));
            });
            
            // Combo-Break Debugger: Request-scoped checkpoint tracker
            services.AddScoped<ICheckpointTracker, CheckpointTracker>();
            
            // Combo-Break Debugger: DAC layer tracking executor (TASK-007)
            services.AddScoped<Persistence.TrackedDacExecutor>();
            
            // Combo-Break Debugger: Bind configuration options
            services.Configure<Core.Configuration.ComboBreakDebuggerOptions>(
                configuration.GetSection(Core.Configuration.ComboBreakDebuggerOptions.SectionName));
            
            // Combo-Break Debugger: Phase 2 - In-memory recorder (dev-only)
            // Production uses NullComboBreakRecorder (zero memory overhead)
            services.AddSingleton<IComboBreakRecorder>(sp =>
            {
                var env = sp.GetRequiredService<IHostEnvironment>();
                return env.IsDevelopment()
                    ? new Debugging.ComboBreakRecorder()
                    : new Debugging.NullComboBreakRecorder();
            });
            
            // Combo-Break Debugger: Phase 3 - Combo map provider
            // In Development: Use ExampleComboMapProvider for testing
            // In Production: Use NullComboMapProvider (no combos = no overhead)
            // Solutions can override with their own AppComboMapProvider
            services.AddSingleton<IComboMapProvider>(sp =>
            {
                var env = sp.GetRequiredService<IHostEnvironment>();
                return env.IsDevelopment()
                    ? new Debugging.ExampleComboMapProvider()
                    : new NullComboMapProvider();
            });
            
            // Combo-Break Debugger: Configure ActivitySource (Phase 3)
            var debuggerOptions = configuration
                .GetSection(Core.Configuration.ComboBreakDebuggerOptions.SectionName)
                .Get<Core.Configuration.ComboBreakDebuggerOptions>() 
                ?? new Core.Configuration.ComboBreakDebuggerOptions();
            Observability.BusinessActivitySource.Configure(debuggerOptions);

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
            services.AddScoped<Core.Abstractions.Identity.IRoleReadDac, RoleReadDac>();
            services.AddScoped<Core.Abstractions.Identity.IRoleWriteDac, RoleWriteDac>();
            
            // Register Identity Services
            services.AddScoped<Core.Abstractions.Identity.IIdentitySyncService, IdentitySyncService>();
            
            // Register Auth Services (JWT Token Generation)
            services.AddSingleton<Services.Auth.ITokenService, Services.Auth.TokenService>();

            // Current User Context — reads from JWT claims (x-local-user-id, sub, email, tid)
            services.AddScoped<Core.Abstractions.Identity.ICurrentUser, CurrentUser>();
            
            // Register Portal Routing DACs
            services.AddScoped<Core.Abstractions.PortalRouting.IClientReadDac, Persistence.Dac.PortalRouting.ClientReadDac>();
            services.AddScoped<Core.Abstractions.PortalRouting.IProjectReadDac, Persistence.Dac.PortalRouting.ProjectReadDac>();
            services.AddScoped<Core.Abstractions.PortalRouting.IClientProjectMappingReadDac, Persistence.Dac.PortalRouting.ClientProjectMappingReadDac>();
            services.AddScoped<Core.Abstractions.PortalRouting.IClientProjectMappingWriteDac, Persistence.Dac.PortalRouting.ClientProjectMappingWriteDac>();
            services.AddScoped<Core.Abstractions.PortalRouting.IClientWriteDac, Persistence.Dac.PortalRouting.ClientWriteDac>();
            services.AddScoped<Core.Abstractions.PortalRouting.IProjectWriteDac, Persistence.Dac.PortalRouting.ProjectWriteDac>();
            
            // Register Task Allocation DACs
            services.AddScoped<Core.Abstractions.TaskAllocation.IProjectAssignmentReadDac, Persistence.Dac.TaskAllocation.ProjectAssignmentReadDac>();
            services.AddScoped<Core.Abstractions.TaskAllocation.ITaskAllocationWriteDac, Persistence.Dac.TaskAllocation.TaskAllocationWriteDac>();

            // Register Dashboard DACs
            services.AddScoped<Core.Abstractions.Dashboard.IDashboardReadDac, Persistence.Dac.Dashboard.DashboardReadDac>();

            // Register Feature Flag DACs
            services.AddScoped<Core.Abstractions.Features.IFeatureReadDac, Persistence.Dac.Features.FeatureReadDac>();
            services.AddScoped<Core.Abstractions.Features.IFeatureWriteDac, Persistence.Dac.Features.FeatureWriteDac>();

            // Register Feature Gate Service
            services.AddScoped<Core.Abstractions.Features.IFeatureGate, Services.Features.FeatureGate>();

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