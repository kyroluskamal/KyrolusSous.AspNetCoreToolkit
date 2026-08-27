using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.MagicLink;

/// <summary>
/// Storage-agnostic persistence contract for storing and atomically consuming magic link tokens.
/// </summary>
public interface IKyrolusMagicLinkStore
{
    Task SaveTokenAsync(
        string tokenHash,
        string userId,
        string email,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically retrieves and deletes/consumes the token to prevent replay attacks.
    /// Returns null if token was not found or was already consumed.
    /// </summary>
    Task<KyrolusMagicLinkRecord?> ConsumeTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges all expired tokens from storage. Returns the count of purged tokens.
    /// </summary>
    Task<int> PurgeExpiredTokensAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service contract for generating and verifying passwordless magic links.
/// </summary>
public interface IKyrolusMagicLinkService
{
    Task<KyrolusMagicLinkCreationResult> CreateMagicLinkAsync(
        KyrolusAuthUser user,
        string baseCallbackUrl,
        TimeSpan? customLifetime = null,
        CancellationToken cancellationToken = default);

    Task<KyrolusMagicLinkValidationResult> ValidateAndConsumeAsync(
        string rawToken,
        CancellationToken cancellationToken = default);
}
