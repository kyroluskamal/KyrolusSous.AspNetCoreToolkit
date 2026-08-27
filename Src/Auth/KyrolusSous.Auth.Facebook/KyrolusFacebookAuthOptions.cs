using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Facebook;

/// <summary>
/// Configuration options for Facebook Login authentication.
/// </summary>
public sealed class KyrolusFacebookAuthOptions : KyrolusExternalLoginOptions
{
    /// <summary>
    /// Initializes a new instance seeded with the standard Facebook Login scopes.
    /// </summary>
    public KyrolusFacebookAuthOptions()
    {
        Scopes = ["email", "public_profile"];
    }

    /// <summary>
    /// Gets or sets the Facebook App ID.
    /// </summary>
    public string AppId { get; set; } = "";

    /// <summary>
    /// Gets or sets the Facebook App Secret.
    /// </summary>
    public string AppSecret { get; set; } = "";

    /// <summary>
    /// Gets or sets the fields to request from the Facebook Graph API. Anything requested here
    /// that is not already mapped by the handler still needs a matching entry in
    /// <see cref="KyrolusExternalLoginOptions.ClaimMappings"/> to reach the principal.
    /// </summary>
    public IList<string> Fields { get; set; } = ["name", "email", "picture"];

    /// <summary>
    /// Gets or sets whether to send the <c>appsecret_proof</c> parameter with Graph API calls.
    /// Defaults to <c>true</c>: it stops a leaked access token from being replayed from anywhere
    /// but the application server, and Facebook can be configured to require it.
    /// </summary>
    public bool SendAppSecretProof { get; set; } = true;
}
