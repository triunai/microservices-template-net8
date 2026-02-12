using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Features;

public static class SetUserOverride
{
    public sealed record Command(
        Guid UserId,
        string FeatureCode,
        string OverrideState,
        string? Reason,
        Guid CreatedBy
    ) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithErrorCode("USER_ID_REQUIRED");

            RuleFor(x => x.FeatureCode)
                .NotEmpty().WithErrorCode("FEATURE_CODE_REQUIRED");

            RuleFor(x => x.OverrideState)
                .NotEmpty().WithErrorCode("OVERRIDE_STATE_REQUIRED")
                .Must(state => FeatureFlagConstants.OverrideStates.All.Contains(state))
                .WithErrorCode("OVERRIDE_STATE_INVALID")
                .WithMessage($"Override state must be one of: {string.Join(", ", FeatureFlagConstants.OverrideStates.All)}");
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IFeatureReadDac _featureReadDac;
        private readonly IFeatureWriteDac _featureWriteDac;
        private readonly IUserReadDac _userReadDac;
        private readonly IFeatureGate _featureGate;

        public Handler(
            IFeatureReadDac featureReadDac,
            IFeatureWriteDac featureWriteDac,
            IUserReadDac userReadDac,
            IFeatureGate featureGate)
        {
            _featureReadDac = featureReadDac;
            _featureWriteDac = featureWriteDac;
            _userReadDac = userReadDac;
            _featureGate = featureGate;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            // 0. Validate
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").ToList();
                errors.Insert(0, ErrorCatalog.VALIDATION_ERROR);
                return Result.Fail(errors);
            }

            // 1. Look up feature by code
            var code = request.FeatureCode.Trim().ToUpperInvariant();
            var feature = await _featureReadDac.GetByCodeAsync(code, ct);
            if (feature is null)
            {
                return Result.Fail(ErrorCatalog.FEATURE_NOT_FOUND);
            }

            // 2. Verify user exists (is_deleted = FALSE filtered by DAC)
            var user = await _userReadDac.GetByIdAsync(request.UserId, ct);
            if (user is null)
            {
                return Result.Fail(ErrorCatalog.USER_NOT_FOUND);
            }

            // 3. Set override
            await _featureWriteDac.SetUserOverrideAsync(
                request.UserId, feature.Id,
                request.OverrideState, request.Reason,
                request.CreatedBy, ct);

            // 4. Invalidate cache
            _featureGate.InvalidateUserOverride(request.UserId, feature.Id);

            return Result.Ok();
        }
    }
}
