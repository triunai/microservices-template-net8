using MicroservicesBase.Core.Abstractions;
using MicroservicesBase.Core.Constants;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MicroservicesBase.Core.Abstractions.Tenancy;
using Dapper;


namespace MicroservicesBase.Infrastructure.Persistence
{
    public sealed class SalesReadDac(ITenantConnectionFactory connFactory, ITenantProvider tenant)
        : ISalesReadDac
    {
        public async Task<SaleReadModel?> GetByIdAsync(Guid saleId, CancellationToken ct)
        {
            await using var conn = new SqlConnection(
                await connFactory.GetSqlConnectionStringAsync(tenant.Id!, ct));
            await conn.OpenAsync(ct);

            using var multi = await conn.QueryMultipleAsync(
                StoredProcedureNames.GetSaleWithItems, // mutli result set return, one "head" + one "items"
                new { SaleId = saleId },
                commandType: CommandType.StoredProcedure);

            var head = await multi.ReadFirstOrDefaultAsync<_Head>();
            if (head is null) return null;

            // todo: add validation for null head to exit and show error

            var items = (await multi.ReadAsync<_Item>()).Select(i => new SaleReadItem(i.Sku, i.Qty, i.UnitPrice)).ToList();

            // todo: add validation if GOT header but no items
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
        }

        // dapper row models (local to persistence)
        // maybe sit this at domain(where its supposed to be)
        private sealed record _Head(
            Guid Id, string TenantId, string StoreId, string RegisterId, string ReceiptNumber,
            DateTimeOffset CreatedAt, decimal NetTotal, decimal TaxTotal, decimal GrandTotal);
        private sealed record _Item(string Sku, int Qty, decimal UnitPrice);
    }
}
