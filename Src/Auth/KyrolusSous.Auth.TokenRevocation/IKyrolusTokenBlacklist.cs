namespace KyrolusSous.Auth.TokenRevocation;

/// <summary>
/// Storage-agnostic persistence contract for blacklisting revoked JWT tokens and user sessions.
/// Can be implemented over Redis, Memory, Distributed Cache, or Database.
/// </summary>
public interface IKyrolusTokenBlacklist
{
    /// <summary>
    /// Revokes a specific JWT token by its unique identifier (<c>jti</c>) until its natural expiration.
    /// </summary>
    Task RevokeTokenAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a specific JWT token's <c>jti</c> has been revoked.
    /// </summary>
    Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all tokens issued to a user prior to <paramref name="revokedBeforeUtc"/> (e.g. on password change or global logout).
    /// </summary>
    Task RevokeUserTokensAsync(string userId, DateTimeOffset revokedBeforeUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a user's token issued at <paramref name="tokenIssuedAtUtc"/> has been superseded by a user-wide revocation.
    /// </summary>
    Task<bool> IsUserTokenRevokedAsync(string userId, DateTimeOffset tokenIssuedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges all expired revoked JTI entries from the blacklist. Returns the count of purged entries.
    /// </summary>
    Task<int> PurgeExpiredRevocationsAsync(CancellationToken cancellationToken = default);
}
