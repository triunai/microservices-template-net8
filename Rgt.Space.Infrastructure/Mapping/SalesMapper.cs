using Riok.Mapperly.Abstractions;
using Rgt.Space.Core.Abstractions;
using Rgt.Space.Core.Domain.Contracts.Sales;

namespace Rgt.Space.Infrastructure.Mapping;

/// <summary>
/// Compile-time mapper for Sales domain using Mapperly.
/// Zero runtime overhead - generates mapping code at compile time.
/// No reflection, no AutoMapper overhead, just pure generated C# code.
/// </summary>
[Mapper]
public partial class SalesMapper
{
    /// <summary>
    /// Maps SaleReadModel (persistence) → SaleResponse (API contract).
    /// Mapperly auto-generates this method at compile time!
    /// All properties are mapped automatically by name/type matching.
    /// </summary>
    public partial SaleResponse ToResponse(SaleReadModel source);
    
    /// <summary>
    /// Maps SaleReadItem (persistence) → SaleResponse.Item (API contract).
    /// Manual mapping since Subtotal is a calculated property (Qty * UnitPrice).
    /// </summary>
    private SaleResponse.Item ToResponseItem(SaleReadItem source) =>
        new(source.Sku, source.Qty, source.UnitPrice, source.Qty * source.UnitPrice);
}

