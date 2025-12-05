using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Rgt.Space.Infrastructure.Services.Auth;

/// <summary>
/// JWT token service for local authentication.
/// Issues access tokens and refresh tokens for local login.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates JWT access and refresh tokens for a user.
    /// </summary>
    TokenResult GenerateTokens(Guid userId, string email, string displayName, IEnumerable<string>? roles = null);
    
    /// <summary>
    /// Generates a secure random refresh token.
    /// </summary>
    string GenerateRefreshToken();
}

public record TokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    DateTime RefreshTokenExpiry
);

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _signingKey;
    private readonly int _accessTokenExpiryMinutes;
    private readonly int _refreshTokenExpiryDays;

    public TokenService(IConfiguration config)
    {
        _config = config;
        
        // Read from configuration with sensible defaults
        _issuer = _config["LocalAuth:Issuer"] ?? "rgt-space-portal";
        _audience = _config["LocalAuth:Audience"] ?? "rgt-space-portal-api";
        _signingKey = _config["LocalAuth:SigningKey"] ?? GenerateDefaultKey();
        _accessTokenExpiryMinutes = int.Parse(_config["LocalAuth:AccessTokenExpiryMinutes"] ?? "60");
        _refreshTokenExpiryDays = int.Parse(_config["LocalAuth:RefreshTokenExpiryDays"] ?? "7");
    }

    public TokenResult GenerateTokens(Guid userId, string email, string displayName, IEnumerable<string>? roles = null)
    {
        var now = DateTime.UtcNow;
        var accessTokenExpiry = now.AddMinutes(_accessTokenExpiryMinutes);
        var refreshTokenExpiry = now.AddDays(_refreshTokenExpiryDays);

        // Build claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("uid", userId.ToString()) // Custom claim for easy access
        };

        // Add role claims
        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        // Create signing credentials
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Create the JWT
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: accessTokenExpiry,
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        return new TokenResult(accessToken, refreshToken, accessTokenExpiry, refreshTokenExpiry);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string GenerateDefaultKey()
    {
        // Generate a secure default key for development
        // WARNING: In production, this MUST be set via configuration/secrets
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
