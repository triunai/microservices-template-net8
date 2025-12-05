using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.Utilities;
using Rgt.Space.Infrastructure.Services.Auth;

namespace Rgt.Space.Infrastructure.Commands.Auth;

public static class Login
{
    public record Command(
        string Email,
        string Password
    ) : IRequest<Result<LoginResponse>>;

    public record LoginResponse(
        string AccessToken,
        string RefreshToken,
        DateTime ExpiresAt,
        Guid UserId,
        string DisplayName,
        string Email
    );

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithErrorCode("EMAIL_REQUIRED")
                .EmailAddress().WithErrorCode("EMAIL_FORMAT_INVALID");

            RuleFor(x => x.Password)
                .NotEmpty().WithErrorCode("PASSWORD_REQUIRED");
        }
    }

    public class Handler : IRequestHandler<Command, Result<LoginResponse>>
    {
        private readonly IUserReadDac _userDac;
        private readonly ITokenService _tokenService;
        private readonly IRoleReadDac _roleDac;
        private readonly ILogger<Handler> _logger;

        public Handler(
            IUserReadDac userDac,
            ITokenService tokenService,
            IRoleReadDac roleDac,
            ILogger<Handler> logger)
        {
            _userDac = userDac;
            _tokenService = tokenService;
            _roleDac = roleDac;
            _logger = logger;
        }

        public async Task<Result<LoginResponse>> Handle(Command request, CancellationToken ct)
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

            // 1. Look up user credentials by email
            var user = await _userDac.GetCredentialsByEmailAsync(request.Email, ct);
            
            if (user is null)
            {
                _logger.LogWarning("Login failed: User not found for email {Email}", request.Email);
                return Result.Fail(ErrorCatalog.INVALID_CREDENTIALS);
            }

            // 2. Check if account is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: Account disabled for user {UserId}", user.Id);
                return Result.Fail(ErrorCatalog.ACCOUNT_DISABLED);
            }

            // 3. Check if local login is enabled
            if (!user.LocalLoginEnabled)
            {
                _logger.LogWarning("Login failed: Local login disabled for user {UserId}", user.Id);
                return Result.Fail(ErrorCatalog.LOCAL_LOGIN_DISABLED);
            }

            // 4. Verify password hash exists
            if (user.PasswordHash is null || user.PasswordSalt is null)
            {
                _logger.LogWarning("Login failed: No password set for user {UserId}", user.Id);
                return Result.Fail(ErrorCatalog.INVALID_CREDENTIALS);
            }

            // 5. Verify password
            if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                _logger.LogWarning("Login failed: Invalid password for user {UserId}", user.Id);
                return Result.Fail(ErrorCatalog.INVALID_CREDENTIALS);
            }

            // 6. Check password expiry
            if (user.PasswordExpiryAt.HasValue && user.PasswordExpiryAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Login failed: Password expired for user {UserId}", user.Id);
                return Result.Fail(ErrorCatalog.PASSWORD_EXPIRED);
            }

            // 7. Get user roles for token claims
            var userRoles = await _roleDac.GetUserRolesAsync(user.Id, ct);
            var roleCodes = userRoles.Select(r => r.RoleCode).ToList();

            // 8. Generate tokens
            var tokens = _tokenService.GenerateTokens(
                user.Id,
                user.Email,
                user.DisplayName,
                roleCodes);

            _logger.LogInformation("Login successful for user {UserId} ({Email})", user.Id, user.Email);

            // TODO: Store refresh token in user_sessions table for refresh token rotation
            // This is optional for now but recommended for production

            return Result.Ok(new LoginResponse(
                tokens.AccessToken,
                tokens.RefreshToken,
                tokens.AccessTokenExpiry,
                user.Id,
                user.DisplayName,
                user.Email
            ));
        }
    }
}
