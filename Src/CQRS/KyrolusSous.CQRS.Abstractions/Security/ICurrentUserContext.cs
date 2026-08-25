using System.Security.Claims;

namespace KyrolusSous.CQRS.Abstractions.Security;

/// <summary>
/// Provides access to the current authenticated user identity and security claims.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// Gets the unique identifier of the current user, or <c>null</c> if unauthenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the display name or username of the current user.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Gets the tenant identifier associated with the current user or ambient context.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets a value indicating whether the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the underlying <see cref="ClaimsPrincipal"/> representing the user.
    /// </summary>
    ClaimsPrincipal? User { get; }

    /// <summary>
    /// Gets the roles assigned to the current user.
    /// </summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    /// Gets the custom permissions granted to the current user.
    /// </summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>
    /// Checks whether the user is in the specified role.
    /// </summary>
    bool IsInRole(string role);

    /// <summary>
    /// Checks whether the user has the specified permission.
    /// </summary>
    bool HasPermission(string permission);
}
