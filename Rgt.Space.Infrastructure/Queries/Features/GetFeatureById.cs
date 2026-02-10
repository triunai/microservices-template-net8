using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Features;

public static class GetFeatureById
{
    public sealed record Query(Guid FeatureId) : IRequest<Result<FeatureReadModel>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.FeatureId)
                .NotEmpty()
                .WithErrorCode("FEATURE_ID_REQUIRED")
                .WithMessage("Feature ID is required");
        }
    }

    public sealed class Handler : IRequestHandler<Query, Result<FeatureReadModel>>
    {
        private readonly IFeatureReadDac _dac;

        public Handler(IFeatureReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<FeatureReadModel>> Handle(Query request, CancellationToken ct)
        {
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Result.Fail<FeatureReadModel>(
                    validationResult.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR"));
            }

            var feature = await _dac.GetByIdAsync(request.FeatureId, ct);
            if (feature is null)
            {
                return Result.Fail<FeatureReadModel>(ErrorCatalog.FEATURE_NOT_FOUND);
            }

            return Result.Ok(feature);
        }
    }
}
