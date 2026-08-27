namespace KyrolusSous.Auth.OpenIddict.Options;

/// <summary>
/// How a resource server checks the tokens it receives.
/// </summary>
public enum KyrolusTokenValidationMode
{
    /// <summary>
    /// Validate locally against the signing keys published at the issuer's JWKS endpoint.
    /// Fast (no network call per request after the keys are cached) but cannot see a revocation
    /// until the token expires. Requires the authorization server to issue self-contained,
    /// unencrypted access tokens, or the resource server to hold the encryption key.
    /// </summary>
    Local = 0,

    /// <summary>
    /// Ask the authorization server about every token through its introspection endpoint.
    /// Sees revocations immediately and works with reference tokens, at the cost of a
    /// round trip per token (which OpenIddict caches for the token's remaining lifetime).
    /// </summary>
    Introspection = 1,
}

/// <summary>
/// Configuration for a resource server (API) that accepts tokens issued by a Kyrolus OpenIddict
/// authorization server.
/// </summary>
public sealed class KyrolusOpenIddictApiOptions
{
    /// <summary>
    /// Gets or sets the issuer URL - the address of the authorization server.
    /// </summary>
    public string Issuer { get; set; } = "";

    /// <summary>
    /// Gets or sets how tokens are validated. Defaults to
    /// <see cref="KyrolusTokenValidationMode.Local"/>.
    /// </summary>
    public KyrolusTokenValidationMode ValidationMode { get; set; } = KyrolusTokenValidationMode.Local;

    /// <summary>
    /// Gets or sets the client id this API presents to the introspection endpoint.
    /// Required when <see cref="ValidationMode"/> is
    /// <see cref="KyrolusTokenValidationMode.Introspection"/>.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret for introspection. Required when <see cref="ValidationMode"/>
    /// is <see cref="KyrolusTokenValidationMode.Introspection"/>.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the audiences this API answers to. A token whose <c>aud</c> does not include
    /// one of these is rejected.
    /// </summary>
    /// <remarks>
    /// Leaving this empty means any token from the issuer is accepted, so a token minted for a
    /// different API in the same estate would pass. Set it on anything that is not a single-API
    /// deployment.
    /// </remarks>
    public IList<string> Audiences { get; set; } = [];

    /// <summary>
    /// Gets the encryption certificate this API uses to open encrypted access tokens.
    /// Needed only when validating locally and the authorization server encrypts its tokens.
    /// </summary>
    public KyrolusOpenIddictCertificateOptions EncryptionCertificate { get; } = new();

    /// <summary>
    /// Gets or sets whether to accept requests over plain HTTP. Defaults to <c>false</c>.
    /// Only ever set this in development.
    /// </summary>
    public bool DisableTransportSecurityRequirement { get; set; }

    /// <summary>
    /// Gets or sets whether OpenIddict becomes the default authentication scheme for the
    /// application. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Set it to <c>false</c> when the API also serves cookie-authenticated pages and you want to
    /// pick the scheme per endpoint instead.
    /// </remarks>
    public bool SetAsDefaultScheme { get; set; } = true;
}
