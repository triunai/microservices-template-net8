using MicroservicesBase.Core.Abstractions;
using MicroservicesBase.Core.Configuration;
using MicroservicesBase.Core.Constants;
using MicroservicesBase.Infrastructure.Resilience;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MicroservicesBase.Core.Abstractions.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using Dapper;


namespace MicroservicesBase.Infrastructure.Persistence
{
    public sealed class SalesReadDac : ISalesReadDac
    {
        private readonly ITenantConnectionFactory _connFactory;
        private readonly ITenantProvider _tenant;
        private readonly ResiliencePipelineRegistry<string> _pipelineRegistry;
        private readonly IOptions<ResilienceSettings> _resilienceSettings;
        private readonly ILogger<SalesReadDac> _logger;

        public SalesReadDac(
            ITenantConnectionFactory connFactory, 
            ITenantProvider tenant,
            ResiliencePipelineRegistry<string> pipelineRegistry,
            IOptions<ResilienceSettings> resilienceSettings,
            ILogger<SalesReadDac> logger)
        {
            _connFactory = connFactory;
            _tenant = tenant;
            _pipelineRegistry = pipelineRegistry;
            _resilienceSettings = resilienceSettings;
            _logger = logger;
        }

        public async Task<SaleReadModel?> GetByIdAsync(Guid saleId, CancellationToken ct)
        {
            var tenantId = _tenant.Id!;
            
            // Get or create pipeline for this tenant on-demand
            var pipelineKey = tenantId; // Just use tenant ID as key
            if (!_pipelineRegistry.TryGetPipeline(pipelineKey, out var pipeline))
            {
                // Create pipeline on first access
                _pipelineRegistry.TryAddBuilder(pipelineKey, (builder, context) =>
                {
                    var settings = _resilienceSettings.Value.TenantDb;
                    builder.AddPipelineFromSettings(
                        settings,
                        ResiliencePolicies.IsSqlTransientError,
                        $"TenantDb:{tenantId}",
                        _logger);
                });
                pipeline = _pipelineRegistry.GetPipeline(pipelineKey);
            }
            
            _logger.LogDebug("Querying sale {SaleId} for tenant {TenantId}", saleId, tenantId);
            
            // Get connection string OUTSIDE the TenantDb pipeline (Redis failures shouldn't count against SQL query time)
            var connectionString = await _connFactory.GetSqlConnectionStringAsync(tenantId, ct);
            
            // TenantDb pipeline wraps ONLY the SQL query (not connection resolution)
            return await pipeline.ExecuteAsync(async token =>
            {
                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync(token); // Propagate cancellation token

                using var multi = await conn.QueryMultipleAsync(
                    StoredProcedureNames.GetSaleWithItems, // multi result set return, one "head" + one "items"
                    new { SaleId = saleId },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: SqlConstants.CommandTimeouts.TenantDb);

                var head = await multi.ReadFirstOrDefaultAsync<_Head>();
                if (head is null)
                {
                    _logger.LogDebug("Sale {SaleId} not found for tenant {TenantId}", saleId, tenantId);
                    return null;
                }

                var items = (await multi.ReadAsync<_Item>())
                    .Select(i => new SaleReadItem(i.Sku, i.Qty, i.UnitPrice))
                    .ToList();

                _logger.LogDebug("Successfully retrieved sale {SaleId} with {ItemCount} items for tenant {TenantId}", 
                    saleId, items.Count, tenantId);

                return new SaleReadModel(
                    head.Id,
                    head.TenantId,
                    head.StoreId,
                    head.RegisterId,
                    head.ReceiptNumber,
                    head.CreatedAt,
                    head.NetTotal,
                    head.TaxTotal,
                    head.GrandTotal,
                    items);
            }, ct);
        }

        // dapper row models (local to persistence)
        // maybe sit this at domain(where its supposed to be)
        private sealed record _Head(
            Guid Id, string TenantId, string StoreId, string RegisterId, string ReceiptNumber,
            DateTimeOffset CreatedAt, decimal NetTotal, decimal TaxTotal, decimal GrandTotal);
        private sealed record _Item(string Sku, int Qty, decimal UnitPrice);
    }
}
