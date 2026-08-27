using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Apple;

/// <summary>
/// Configuration options for Sign in with Apple authentication.
/// </summary>
public sealed class KyrolusAppleAuthOptions : KyrolusExternalLoginOptions
{
    /// <summary>
    /// Initializes a new instance seeded with the name and email scopes.
    /// </summary>
    public KyrolusAppleAuthOptions()
    {
        Scopes = ["name", "email"];
    }

    /// <summary>
    /// Gets or sets the Apple Service ID, which acts as the OAuth client id.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Gets or sets the Apple Team ID (the 10-character identifier from the developer portal).
    /// </summary>
    public string TeamId { get; set; } = "";

    /// <summary>
    /// Gets or sets the Apple Key ID that identifies the signing key.
    /// </summary>
    public string KeyId { get; set; } = "";

    /// <summary>
    /// Gets or sets the path to the Apple AuthKey (<c>.p8</c>) private key file.
    /// Mutually exclusive with <see cref="PrivateKeyPem"/>.
    /// </summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// Gets or sets the contents of the Apple AuthKey (<c>.p8</c>) private key, in PKCS#8 PEM form.
    /// Use this when the key arrives from a secret store rather than the file system.
    /// Mutually exclusive with <see cref="PrivateKeyPath"/>.
    /// </summary>
    public string? PrivateKeyPem { get; set; }

    /// <summary>
    /// Gets or sets how long a generated client secret stays valid. Apple caps this at six months
    /// and rejects anything longer. Defaults to 6 months.
    /// </summary>
    public TimeSpan ClientSecretExpiresAfter { get; set; } = TimeSpan.FromDays(180);

    /// <summary>
    /// Gets or sets whether to validate the identity token Apple returns against its published
    /// keys. Defaults to <c>true</c>; there is no good reason to turn it off outside a test rig.
    /// </summary>
    public bool ValidateTokens { get; set; } = true;
}
