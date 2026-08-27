using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.MicrosoftAccount;

/// <summary>
/// Configuration options for Microsoft Account / Microsoft Entra ID authentication.
/// </summary>
public sealed class KyrolusMicrosoftAuthOptions : KyrolusExternalLoginOptions
{
    /// <summary>
    /// Initializes a new instance seeded with the Microsoft Graph scope that returns the
    /// signed-in profile.
    /// </summary>
    public KyrolusMicrosoftAuthOptions()
    {
        Scopes = ["https://graph.microsoft.com/user.read"];
    }

    /// <summary>
    /// Gets or sets the application (client) ID from the Entra ID app registration.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Gets or sets the client secret from the Entra ID app registration.
    /// </summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Gets or sets which Entra ID tenant may sign in. Defaults to <c>"common"</c> (any work,
    /// school or personal Microsoft account).
    /// </summary>
    /// <remarks>
    /// Use a tenant GUID or a verified domain to restrict a line-of-business application to one
    /// organisation, <c>"organizations"</c> for any work or school account, or
    /// <c>"consumers"</c> for personal accounts only. Unlike a client-side hint this is enforced
    /// by the token endpoint the request is sent to, so it cannot be bypassed by the browser.
    /// </remarks>
    public string Tenant { get; set; } = "common";

    /// <summary>
    /// Gets or sets the <c>prompt</c> parameter (<c>none</c>, <c>login</c>, <c>consent</c>,
    /// <c>select_account</c>).
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Gets or sets the <c>domain_hint</c> parameter, which skips the account-type chooser
    /// and sends the user straight to the identity provider for that domain.
    /// </summary>
    public string? DomainHint { get; set; }
}
