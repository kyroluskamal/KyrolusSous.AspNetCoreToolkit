namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Validates a username/password pair and applies the surrounding sign-in policy
/// (account state, email confirmation, brute-force lockout).
/// </summary>
/// <remarks>
/// Kept separate from <see cref="IKyrolusAuthUserStore"/> on purpose: the store answers "who is
/// this?", the authenticator answers "may they sign in right now?". Applications routinely need
/// to replace the second without touching the first.
/// </remarks>
public interface IKyrolusUserAuthenticator
{
    /// <summary>
    /// Authenticates a user by their login name or email address.
    /// </summary>
    /// <param name="userNameOrEmail">The identifier the user typed.</param>
    /// <param name="password">The plaintext password the user typed.</param>
    /// <param name="cancellationToken">A token that aborts the operation.</param>
    ValueTask<KyrolusAuthenticationResult> AuthenticateAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of a credential check.
/// </summary>
public sealed class KyrolusAuthenticationResult
{
    private KyrolusAuthenticationResult(
        bool succeeded,
        KyrolusAuthUser? user,
        string? errorCode,
        string? errorDescription)
    {
        Succeeded = succeeded;
        User = user;
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
    }

    /// <summary>Gets whether the credentials were accepted.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the authenticated user, when <see cref="Succeeded"/> is <c>true</c>.</summary>
    public KyrolusAuthUser? User { get; }

    /// <summary>Gets the error code, from <see cref="KyrolusAuthConstants.Errors"/>.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets a human-readable failure reason safe to return to the caller.</summary>
    public string? ErrorDescription { get; }

    /// <summary>Creates a successful result.</summary>
    public static KyrolusAuthenticationResult Success(KyrolusAuthUser user)
        => new(true, user, null, null);

    /// <summary>Creates a failed result.</summary>
    public static KyrolusAuthenticationResult Fail(string errorCode, string errorDescription)
        => new(false, null, errorCode, errorDescription);
}
