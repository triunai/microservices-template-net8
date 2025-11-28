using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Domain.Entities
{
    public sealed class Sale
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        // tenancy/contextual info (plain strings, validated elsewhere)
        public string TenantId { get; private set; } = default!;
        public string StoreId { get; private set; } = default!;
        public string RegisterId { get; private set; } = default!;

        // business
        public string ReceiptNumber { get; private set; } = default!;
        public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

        public decimal NetTotal { get; private set; }     // before tax/discount
        public decimal TaxTotal { get; private set; }     // set by strategy/handler
        public decimal GrandTotal { get; private set; }   // Net + Tax - Discount

        private readonly List<SaleItem> _items = new();
        public IReadOnlyList<SaleItem> Items => _items;

        private Sale() { } // EF/Dapper-friendly

        public static Sale Create(
            string tenantId,
            string storeId,
            string registerId,
            string receiptNumber,
            IEnumerable<SaleItem> items,
            DateTimeOffset? now = null)
        {
            var sale = new Sale
            {
                TenantId = tenantId,
                StoreId = storeId,
                RegisterId = registerId,
                ReceiptNumber = receiptNumber,
                CreatedAt = now ?? DateTimeOffset.UtcNow,
            };
            sale._items.AddRange(items);
            sale.RecomputeTotals();
            return sale;
        }

        public void ApplyTax(decimal taxAmount)
        {
            TaxTotal = taxAmount < 0 ? 0 : taxAmount;
            RecomputeTotals();
        }

        private void RecomputeTotals()
        {
            NetTotal = _items.Sum(i => i.Subtotal);
            GrandTotal = NetTotal + TaxTotal; // discount later if you add it
            if (GrandTotal < 0) GrandTotal = 0;
        }
    }
}
