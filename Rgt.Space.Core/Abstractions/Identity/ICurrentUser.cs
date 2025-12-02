namespace Rgt.Space.Core.Abstractions.Identity;

/// <summary>
/// Provides access to the current authenticated user's context.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// The Local User ID (Primary Key in 'users' table).
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The SSO Subject ID ('sub' claim).
    /// </summary>
    string? ExternalId { get; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// The Tenant Key ('tid' claim).
    /// </summary>
    string? TenantKey { get; }

    /// <summary>
    /// Indicates if the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
