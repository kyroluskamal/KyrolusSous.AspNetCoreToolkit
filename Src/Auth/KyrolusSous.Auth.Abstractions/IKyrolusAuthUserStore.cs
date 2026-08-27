namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// The single storage seam of the Kyrolus auth stack. Implement it over Entity Framework, Marten,
/// Dapper, MongoDB, an in-memory list, or a remote user service - nothing in these packages knows
/// or cares which.
/// </summary>
/// <remarks>
/// Every method may return <c>null</c> for "no such user"; throwing for a routine miss is not
/// expected. Implementations are resolved as scoped services.
/// </remarks>
public interface IKyrolusAuthUserStore
{
    /// <summary>Finds a user by their stable identifier.</summary>
    ValueTask<KyrolusAuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by login name.</summary>
    ValueTask<KyrolusAuthUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by email address.</summary>
    ValueTask<KyrolusAuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the user linked to an external provider identity.
    /// </summary>
    /// <param name="provider">The provider name, from <see cref="KyrolusAuthConstants.Providers"/>.</param>
    /// <param name="providerKey">The provider's stable identifier for the user.</param>
    /// <param name="cancellationToken">A token that aborts the operation.</param>
    ValueTask<KyrolusAuthUser?> FindByExternalLoginAsync(
        string provider,
        string providerKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new user and returns the stored record, with <see cref="KyrolusAuthUser.Id"/>
    /// populated if the store assigns it.
    /// </summary>
    ValueTask<KyrolusAuthUser> CreateAsync(KyrolusAuthUser user, CancellationToken cancellationToken = default);

    /// <summary>Links an external provider identity to an existing user.</summary>
    ValueTask AddExternalLoginAsync(
        string userId,
        string provider,
        string providerKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional companion to <see cref="IKyrolusAuthUserStore"/> that persists sign-in failure counts.
/// Register an implementation to turn on brute-force lockout; without one the auth endpoints
/// still work, they just do not count failures.
/// </summary>
public interface IKyrolusAuthUserLockoutStore
{
    /// <summary>Records a failed sign-in attempt and persists the resulting counter and lockout window.</summary>
    ValueTask RecordFailedAttemptAsync(
        string userId,
        int accessFailedCount,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken = default);

    /// <summary>Clears the failure counter after a successful sign-in.</summary>
    ValueTask ResetFailedAttemptsAsync(string userId, CancellationToken cancellationToken = default);
}
