using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Core.Domain.Entities
{
    public sealed class SaleItem
    {
        public string Sku { get; private set; } = default!;
        public int Qty { get; private set; }
        public decimal UnitPrice { get; private set; }   // unit price

        public decimal Subtotal => Qty * UnitPrice;

        private SaleItem() { } // EF/Dapper-friendly

        public static SaleItem Create(string sku, int qty, decimal unitPrice)
            => new() { Sku = sku, Qty = qty, UnitPrice = unitPrice };
    }
}
