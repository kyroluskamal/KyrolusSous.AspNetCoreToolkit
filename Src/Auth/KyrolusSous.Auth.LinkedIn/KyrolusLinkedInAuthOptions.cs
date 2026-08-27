using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.LinkedIn;

/// <summary>
/// Configuration options for LinkedIn (Sign In with LinkedIn using OpenID Connect) authentication.
/// </summary>
public sealed class KyrolusLinkedInAuthOptions : KyrolusExternalLoginOptions
{
    /// <summary>
    /// Initializes a new instance seeded with the OpenID Connect scopes LinkedIn expects.
    /// </summary>
    public KyrolusLinkedInAuthOptions()
    {
        Scopes = ["openid", "profile", "email"];
    }

    /// <summary>
    /// Gets or sets the LinkedIn application Client ID.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Gets or sets the LinkedIn application Client Secret.
    /// </summary>
    public string ClientSecret { get; set; } = "";
}
