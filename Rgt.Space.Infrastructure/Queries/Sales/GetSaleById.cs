using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions;
using Rgt.Space.Core.Domain.Contracts.Sales;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rgt.Space.Infrastructure.Queries.Sales
{
    public class GetSaleById
    {
        // 1) The query contract
        public sealed record GetSaleByIdQuery(Guid SaleId) : IRequest<Result<SaleResponse>>;

        // 2) Validation rules (FluentValidation)
        public sealed class Validator : AbstractValidator<GetSaleByIdQuery>
        {
            public Validator()
            {
                // Validate GUID is not empty (00000000-0000-0000-0000-000000000000)
                RuleFor(x => x.SaleId)
                    .NotEqual(Guid.Empty)
                    .WithErrorCode("SALE_ID_INVALID")
                    .WithMessage("Sale ID cannot be empty");
            }
        }
        
        public sealed class Handler : IRequestHandler<GetSaleByIdQuery, Result<SaleResponse>>
        {
            private readonly ISalesReadDac _dac;
            private readonly Mapping.SalesMapper _mapper;
            
            public Handler(ISalesReadDac dac, Mapping.SalesMapper mapper)
            {
                _dac = dac;
                _mapper = mapper;
            }
            
            public async Task<Result<SaleResponse>> Handle(GetSaleByIdQuery q, CancellationToken ct)
            {
                // validate inline (no separate pipeline needed)
                var v = new Validator();
                var vr = await v.ValidateAsync(q, ct);
                if (!vr.IsValid)
                    return Result.Fail<SaleResponse>(vr.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").DefaultIfEmpty("VALIDATION_ERROR").ToArray());

                // fetch from persistence(ensure has try catch for safe error handling)
                var data = await _dac.GetByIdAsync(q.SaleId, ct);
                if (data is null)
                    return Result.Fail<SaleResponse>("SALE_NOT_FOUND");

                //  Map with Mapperly (compile-time, zero-overhead mapping)
                var response = _mapper.ToResponse(data);

                return Result.Ok(response);
            }
        }
    }
}