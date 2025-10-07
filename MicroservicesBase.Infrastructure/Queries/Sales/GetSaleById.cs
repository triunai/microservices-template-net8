using FluentResults;
using FluentValidation;
using MediatR;
using MicroservicesBase.Core.Abstractions;
using MicroservicesBase.Core.Domain.Contracts.Sales;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Infrastructure.Queries.Sales
{
    public class GetSaleById
    {
        // 1) The query contract
        public sealed record GetSaleByIdQuery(Guid SaleId) : IRequest<Result<SaleResponse>>;

        // 2) Validation rules (FluentValidation)
        public sealed class Validator : AbstractValidator<GetSaleByIdQuery>
        {
            // regex checks, validation rules, no need to write unncessary validation in handler layer
            // can write upsert checks( if object exists on handler layer ), otherwise write not null checks, length checks here
            public Validator()
            {
                RuleFor(x => x.SaleId);
                
            }
        }

        public sealed class Handler(ISalesReadDac dac) : IRequestHandler<GetSaleByIdQuery, Result<SaleResponse>>
        {
            public async Task<Result<SaleResponse>> Handle(GetSaleByIdQuery q, CancellationToken ct)
            {
                // validate inline (no separate pipeline needed)
                var v = new Validator();
                var vr = await v.ValidateAsync(q, ct);
                if (!vr.IsValid)
                    return Result.Fail<SaleResponse>(vr.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").DefaultIfEmpty("VALIDATION_ERROR").ToArray());

                // fetch from persistence
                var data = await dac.GetByIdAsync(q.SaleId, ct);
                if (data is null)
                    return Result.Fail<SaleResponse>("SALE_NOT_FOUND");

                // map persistence model -> response
                var items = data.Items
                    .Select(i => new SaleResponse.Item(i.Sku, i.Qty, i.UnitPrice, i.Qty * i.UnitPrice))
                    .ToList();

                var res = new SaleResponse(
                    data.Id,
                    data.TenantId,
                    data.StoreId,
                    data.RegisterId,
                    data.ReceiptNumber,
                    data.CreatedAt,
                    data.NetTotal,
                    data.TaxTotal,
                    data.GrandTotal,
                    items);

                return Result.Ok(res);
            }
        }
    }
}