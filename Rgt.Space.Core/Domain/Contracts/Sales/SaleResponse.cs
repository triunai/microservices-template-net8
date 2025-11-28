using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Rgt.Space.Core.Domain.Contracts.Sales.SaleResponse;

namespace Rgt.Space.Core.Domain.Contracts.Sales
{
    public sealed record SaleResponse(
        Guid Id,
        string TenantId,
        string StoreId,
        string RegisterId,
        string ReceiptNumber,
        DateTimeOffset CreatedAt,
        decimal NetTotal,
        decimal TaxTotal,
        decimal GrandTotal,
        List<Item> Items)
    {
        public sealed record Item(string Sku, int Qty, decimal UnitPrice, decimal Subtotal);
    }
}
