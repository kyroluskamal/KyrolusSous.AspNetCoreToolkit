using KyrolusSous.Auth.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Auth.Runtime;

/// <summary>
/// The default sign-in policy: look the user up, check the password, and refuse the sign-in when
/// the account is disabled, unconfirmed or locked out.
/// </summary>
public sealed class KyrolusUserAuthenticator(
    IKyrolusAuthUserStore userStore,
    IKyrolusPasswordHasher passwordHasher,
    IOptions<KyrolusAuthOptions> options,
    TimeProvider timeProvider,
    IKyrolusAuthUserLockoutStore? lockoutStore = null) : IKyrolusUserAuthenticator
{
    private readonly KyrolusAuthOptions _options = options.Value;

    /// <inheritdoc />
    public async ValueTask<KyrolusAuthenticationResult> AuthenticateAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrEmpty(password) ||
            password.Length > 4096 || userNameOrEmail.Length > 256)
        {
            return InvalidCredentials();
        }

        var trimmedIdentifier = userNameOrEmail.Trim();
        var user = await userStore.FindByUserNameAsync(trimmedIdentifier, cancellationToken).ConfigureAwait(false);

        if (user is null && _options.AllowSignInWithEmail && trimmedIdentifier.Contains('@', StringComparison.Ordinal))
        {
            user = await userStore.FindByEmailAsync(trimmedIdentifier, cancellationToken).ConfigureAwait(false);
        }

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            // Burn a comparable amount of work anyway. Returning early on an unknown user makes
            // the response measurably faster than for a known one, which turns the endpoint into
            // a user-enumeration oracle. Hashing (rather than verifying a canned hash) costs the
            // same as the real path for whatever hasher happens to be plugged in.
            _ = passwordHasher.Hash(password);
            return InvalidCredentials();
        }

        var now = timeProvider.GetUtcNow();
        if (user.IsLockedOut(now))
        {
            return KyrolusAuthenticationResult.Fail(
                KyrolusAuthConstants.Errors.UserLockedOut,
                "The account is temporarily locked because of repeated failed sign-in attempts.");
        }

        var verification = passwordHasher.Verify(user.PasswordHash, password);
        if (verification == KyrolusPasswordVerificationResult.Failed)
        {
            await RecordFailureAsync(user, now, cancellationToken).ConfigureAwait(false);
            return InvalidCredentials();
        }

        if (!user.IsActive)
        {
            return KyrolusAuthenticationResult.Fail(
                KyrolusAuthConstants.Errors.UserInactive,
                "The account is disabled.");
        }

        if (_options.RequireConfirmedEmail && !user.EmailConfirmed)
        {
            return KyrolusAuthenticationResult.Fail(
                KyrolusAuthConstants.Errors.EmailNotConfirmed,
                "The email address on this account has not been confirmed.");
        }

        if (lockoutStore is not null && user.AccessFailedCount > 0)
        {
            await lockoutStore.ResetFailedAttemptsAsync(user.Id, cancellationToken).ConfigureAwait(false);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
        }

        return KyrolusAuthenticationResult.Success(user);
    }

    private async ValueTask RecordFailureAsync(
        KyrolusAuthUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (lockoutStore is null || !user.LockoutEnabled || _options.MaxFailedAccessAttempts <= 0)
        {
            return;
        }

        var failures = user.AccessFailedCount + 1;
        DateTimeOffset? lockoutEnd = failures >= _options.MaxFailedAccessAttempts
            ? now + _options.LockoutDuration
            : null;

        await lockoutStore
            .RecordFailedAttemptAsync(user.Id, failures, lockoutEnd, cancellationToken)
            .ConfigureAwait(false);
    }

    private static KyrolusAuthenticationResult InvalidCredentials()
        => KyrolusAuthenticationResult.Fail(
            KyrolusAuthConstants.Errors.InvalidCredentials,
            "The username or password is incorrect.");
}
