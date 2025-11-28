using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.Identity;

public sealed class UserSession : Entity
{
    public Guid UserId { get; private set; }
    public string RefreshToken { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? CreatedIp { get; private set; }
    public string? DeviceInfo { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedBy { get; private set; }

    private UserSession(Guid id) : base(id) { }

    public static UserSession Create(
        Guid userId,
        string refreshToken,
        DateTime expiresAt,
        string? ipAddress,
        string? deviceInfo)
    {
        return new UserSession(Guid.NewGuid())
        {
            UserId = userId,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedIp = ipAddress,
            DeviceInfo = deviceInfo,
            IsRevoked = false
        };
    }

    public void Revoke(string? replacedByToken = null)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        ReplacedBy = replacedByToken;
    }
}
