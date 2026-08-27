using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.GitHub;

/// <summary>
/// Configuration options for GitHub OAuth authentication.
/// </summary>
public sealed class KyrolusGitHubAuthOptions : KyrolusExternalLoginOptions
{
    /// <summary>
    /// Initializes a new instance seeded with the scopes needed to read a profile and its
    /// email addresses.
    /// </summary>
    public KyrolusGitHubAuthOptions()
    {
        Scopes = ["read:user", "user:email"];
    }

    /// <summary>
    /// Gets or sets the GitHub OAuth App Client ID.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Gets or sets the GitHub OAuth App Client Secret.
    /// </summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Gets or sets the GitHub Enterprise Server domain (for example <c>github.contoso.com</c>).
    /// Leave <c>null</c> for public GitHub.
    /// </summary>
    /// <remarks>
    /// Supply the bare host name only. The GitHub handler derives the authorization, token and
    /// API endpoints from it, including the <c>/api/v3</c> prefix that Enterprise Server uses
    /// instead of the <c>api.github.com</c> host.
    /// </remarks>
    public string? EnterpriseDomain { get; set; }
}
