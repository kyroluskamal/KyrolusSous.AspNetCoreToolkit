using System.Security.Claims;

namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Builds the <see cref="ClaimsPrincipal"/> that gets turned into a token for a given user.
/// Replace the default implementation to control exactly which claims an application issues.
/// </summary>
public interface IKyrolusClaimsPrincipalFactory
{
    /// <summary>
    /// Creates a principal for <paramref name="user"/> restricted to <paramref name="grantedScopes"/>.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="grantedScopes">
    /// The scopes granted by the current request. Implementations use them to decide which claims
    /// to include (for example, only emit <c>email</c> when the <c>email</c> scope was granted).
    /// </param>
    /// <param name="authenticationType">The authentication type stamped on the identity.</param>
    /// <param name="cancellationToken">A token that aborts the operation.</param>
    ValueTask<ClaimsPrincipal> CreateAsync(
        KyrolusAuthUser user,
        IReadOnlyCollection<string> grantedScopes,
        string authenticationType,
        CancellationToken cancellationToken = default);
}
