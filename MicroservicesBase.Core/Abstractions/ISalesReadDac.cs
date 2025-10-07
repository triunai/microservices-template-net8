using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Core.Abstractions
{
    public interface ISalesReadDac
    {
        Task<SaleReadModel?> GetByIdAsync(Guid saleId, CancellationToken ct);
    }
    // Flat read model used by the query handler (no domain, just data)
    public sealed record SaleReadModel(
        Guid Id,
        string TenantId,
        string StoreId,
        string RegisterId,
        string ReceiptNumber,
        DateTimeOffset CreatedAt,
        decimal NetTotal,
        decimal TaxTotal,
        decimal GrandTotal,
        List<SaleReadItem> Items);

    public sealed record SaleReadItem(string Sku, int Qty, decimal UnitPrice);
}
