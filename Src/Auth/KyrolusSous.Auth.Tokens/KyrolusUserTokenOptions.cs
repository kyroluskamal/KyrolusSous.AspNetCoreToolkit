namespace KyrolusSous.Auth.Tokens;

/// <summary>
/// Standard purpose identifiers for user tokens.
/// </summary>
public static class KyrolusTokenPurposes
{
    public const string EmailConfirmation = "EmailConfirmation";
    public const string PasswordReset = "PasswordReset";
    public const string ChangeEmail = "ChangeEmail";
    public const string PhoneVerification = "PhoneVerification";
}

/// <summary>
/// Configuration options for user token generation and validation.
/// </summary>
public sealed class KyrolusUserTokenOptions
{
    /// <summary>
    /// Master application signing key used to sign tokens (minimum 256 bits / 32 characters).
    /// </summary>
    public string SecretKey { get; set; } = "KyrolusSous_Default_UserToken_SecretKey_Minimum_32_Chars!";

    /// <summary>
    /// Default token lifetime if not specified per purpose. Defaults to 24 hours.
    /// </summary>
    public TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Lifetime for password reset tokens. Defaults to 2 hours.
    /// </summary>
    public TimeSpan PasswordResetLifetime { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Lifetime for email confirmation tokens. Defaults to 3 days.
    /// </summary>
    public TimeSpan EmailConfirmationLifetime { get; set; } = TimeSpan.FromDays(3);

    /// <summary>
    /// Clock skew tolerance to accommodate server clock drift. Defaults to <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.Zero;
}
