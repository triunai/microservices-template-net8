using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Domain.Entities.Features;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Features;

public static class CreateFeature
{
    public sealed record Command(
        string Code,
        string Name,
        string? Description,
        bool IsActive,
        bool RequiresClient,
        Guid CreatedBy
    ) : IRequest<Result<Guid>>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithErrorCode("FEATURE_CODE_REQUIRED")
                .MaximumLength(50).WithErrorCode("FEATURE_CODE_TOO_LONG")
                .Matches("^[A-Z][A-Z0-9_]*$").WithErrorCode("FEATURE_CODE_FORMAT_INVALID")
                .WithMessage("Feature code must be UPPER_SNAKE_CASE starting with a letter.");

            RuleFor(x => x.Name)
                .NotEmpty().WithErrorCode("FEATURE_NAME_REQUIRED")
                .MaximumLength(255).WithErrorCode("FEATURE_NAME_TOO_LONG");
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result<Guid>>
    {
        private readonly IFeatureReadDac _readDac;
        private readonly IFeatureWriteDac _writeDac;
        private readonly IFeatureGate _featureGate;

        public Handler(IFeatureReadDac readDac, IFeatureWriteDac writeDac, IFeatureGate featureGate)
        {
            _readDac = readDac;
            _writeDac = writeDac;
            _featureGate = featureGate;
        }

        public async Task<Result<Guid>> Handle(Command request, CancellationToken ct)
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

            // 1. Normalize code and check uniqueness
            var code = request.Code.Trim().ToUpperInvariant();
            var existing = await _readDac.GetByCodeAsync(code, ct);
            if (existing is not null)
            {
                return Result.Fail(ErrorCatalog.FEATURE_CODE_EXISTS);
            }

            // 2. Create entity (single source of ID generation) and persist
            // The try-catch handles the TOCTOU race where two concurrent requests both pass
            // the uniqueness check above but the DB's unique index catches the second INSERT.
            // Without this, the ConflictException from WriteDac propagates as an unhandled exception
            // (caught by GlobalExceptionHandler) — a different error path than the Result.Fail above.
            // This unifies both paths through FluentResults for consistent audit logging and error shape.
            var feature = Feature.Create(code, request.Name, request.Description,
                request.IsActive, request.RequiresClient, request.CreatedBy);
            try
            {
                await _writeDac.CreateFeatureAsync(
                    feature.Id, code, request.Name, request.Description,
                    request.IsActive, request.RequiresClient,
                    request.CreatedBy, ct);
            }
            catch (ConflictException)
            {
                return Result.Fail(ErrorCatalog.FEATURE_CODE_EXISTS);
            }

            // 3. Invalidate negative cache — if someone checked this code before creation,
            // the null result is cached for 60s. Without this, the new feature is invisible
            // to FeatureGate.IsEnabledAsync until the cache entry expires.
            _featureGate.InvalidateFeature(code);

            return Result.Ok(feature.Id);
        }
    }
}
