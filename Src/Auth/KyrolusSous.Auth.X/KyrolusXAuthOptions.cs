using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.X;

/// <summary>
/// Configuration options for X (formerly Twitter) OAuth 2.0 authentication.
/// </summary>
public sealed class KyrolusXAuthOptions : KyrolusExternalLoginOptions
{
    /// <summary>
    /// Initializes a new instance seeded with the scopes X requires to read the signed-in profile.
    /// </summary>
    public KyrolusXAuthOptions()
    {
        Scopes = ["tweet.read", "users.read"];
    }

    /// <summary>
    /// Gets or sets the X OAuth 2.0 Client ID.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Gets or sets the X OAuth 2.0 Client Secret.
    /// </summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Gets or sets whether to request offline access, which is what makes X return a refresh
    /// token. Defaults to <c>false</c>.
    /// </summary>
    public bool RequestRefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the extra user fields to request from the X API (for example
    /// <c>profile_image_url</c>, <c>description</c>, <c>verified</c>).
    /// </summary>
    /// <remarks>
    /// X never returns an email address through OAuth 2.0, whatever fields are requested.
    /// An application that needs one has to collect it separately.
    /// </remarks>
    public IList<string> UserFields { get; set; } = [];
}
