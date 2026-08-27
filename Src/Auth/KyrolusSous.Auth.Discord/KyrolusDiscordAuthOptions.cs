using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Discord;

/// <summary>
/// Configuration options for Discord OAuth 2.0 authentication.
/// </summary>
public sealed class KyrolusDiscordAuthOptions : KyrolusExternalLoginOptions
{
    /// <summary>
    /// Initializes a new instance seeded with the scopes that return the account identity and
    /// its email address.
    /// </summary>
    public KyrolusDiscordAuthOptions()
    {
        Scopes = ["identify", "email"];
    }

    /// <summary>
    /// Gets or sets the Discord application Client ID.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Gets or sets the Discord application Client Secret.
    /// </summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Gets or sets the <c>prompt</c> parameter. Discord accepts <c>consent</c> (the default,
    /// re-asking every time) or <c>none</c> to skip the screen when the user has already
    /// authorised the application.
    /// </summary>
    public string? Prompt { get; set; }
}
