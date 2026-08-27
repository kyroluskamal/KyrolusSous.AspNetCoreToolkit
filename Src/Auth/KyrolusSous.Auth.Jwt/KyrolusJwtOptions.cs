namespace KyrolusSous.Auth.Jwt;

/// <summary>
/// Configuration options for lightweight JWT token generation and validation.
/// </summary>
public sealed class KyrolusJwtOptions
{
    /// <summary>
    /// Gets or sets the symmetric secret key used for signing tokens (minimum 256 bits / 32 characters).
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token issuer.
    /// </summary>
    public string Issuer { get; set; } = "KyrolusSous";

    /// <summary>
    /// Gets or sets the token audience.
    /// </summary>
    public string Audience { get; set; } = "KyrolusSousApi";

    /// <summary>
    /// Gets or sets the lifetime of generated access tokens. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the lifetime of refresh tokens. Defaults to 14 days.
    /// </summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Gets or sets whether to validate the issuer signing key. Defaults to <c>true</c>.
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the token issuer. Defaults to <c>true</c>.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the token audience. Defaults to <c>true</c>.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the token expiration lifetime. Defaults to <c>true</c>.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowable clock drift when validating tokens. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}
