namespace KyrolusSous.Auth.OpenIddict.Options;

/// <summary>
/// Configuration for the Kyrolus OpenIddict Authorization Server.
/// </summary>
/// <remarks>
/// Storage-agnostic by design: nothing here mentions a database. The application configures
/// OpenIddict Core storage itself (EF Core, Marten, MongoDB, Dapper, ...) before calling
/// <c>AddKyrolusOpenIddictAuthServer</c>, and this type covers only protocol behaviour.
/// </remarks>
public sealed class KyrolusOpenIddictOptions
{
    // ── Identity ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the issuer URI advertised in discovery documents and stamped on tokens.
    /// Leave <c>null</c> to let OpenIddict infer it from the incoming request, which is fine
    /// behind a single host but wrong behind a reverse proxy that rewrites it.
    /// </summary>
    public string? Issuer { get; set; }

    // ── Endpoints ────────────────────────────────────────────────────────

    /// <summary>Gets or sets the token endpoint URI. Defaults to <c>"/connect/token"</c>.</summary>
    public string TokenEndpoint { get; set; } = "/connect/token";

    /// <summary>Gets or sets the authorization endpoint URI. Defaults to <c>"/connect/authorize"</c>.</summary>
    public string AuthorizationEndpoint { get; set; } = "/connect/authorize";

    /// <summary>Gets or sets the introspection endpoint URI. Defaults to <c>"/connect/introspect"</c>.</summary>
    public string IntrospectionEndpoint { get; set; } = "/connect/introspect";

    /// <summary>Gets or sets the revocation endpoint URI. Defaults to <c>"/connect/revocation"</c>.</summary>
    public string RevocationEndpoint { get; set; } = "/connect/revocation";

    /// <summary>Gets or sets the userinfo endpoint URI. Defaults to <c>"/connect/userinfo"</c>.</summary>
    public string UserInfoEndpoint { get; set; } = "/connect/userinfo";

    /// <summary>
    /// Gets or sets the end-session (logout) endpoint URI. Defaults to <c>"/connect/logout"</c>.
    /// </summary>
    public string EndSessionEndpoint { get; set; } = "/connect/logout";

    /// <summary>
    /// Gets or sets the device authorization endpoint URI, used only when
    /// <see cref="AllowDeviceAuthorizationFlow"/> is enabled. Defaults to <c>"/connect/device"</c>.
    /// </summary>
    public string DeviceAuthorizationEndpoint { get; set; } = "/connect/device";

    /// <summary>
    /// Gets or sets the end-user verification endpoint URI, where a device-flow user types their
    /// code. Defaults to <c>"/connect/verify"</c>.
    /// </summary>
    public string EndUserVerificationEndpoint { get; set; } = "/connect/verify";

    // ── Flows ────────────────────────────────────────────────────────────

    /// <summary>Gets or sets whether to enable the Authorization Code flow. Defaults to <c>true</c>.</summary>
    public bool AllowAuthorizationCodeFlow { get; set; } = true;

    /// <summary>Gets or sets whether to enable the Refresh Token flow. Defaults to <c>true</c>.</summary>
    public bool AllowRefreshTokenFlow { get; set; } = true;

    /// <summary>Gets or sets whether to enable the Client Credentials flow. Defaults to <c>false</c>.</summary>
    public bool AllowClientCredentialsFlow { get; set; }

    /// <summary>
    /// Gets or sets whether to enable the Resource Owner Password Credentials flow.
    /// Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// OAuth 2.1 removes this grant. It hands the user password to the client application, which
    /// rules out federated sign-in, MFA prompts and step-up authentication. Enable it only for a
    /// first-party client you control, and treat it as a migration step rather than a destination.
    /// </remarks>
    public bool AllowPasswordFlow { get; set; }

    /// <summary>
    /// Gets or sets whether to enable the Implicit flow. Defaults to <c>false</c>.
    /// Removed by OAuth 2.1; use Authorization Code with PKCE instead.
    /// </summary>
    public bool AllowImplicitFlow { get; set; }

    /// <summary>Gets or sets whether to enable the Hybrid flow. Defaults to <c>false</c>.</summary>
    public bool AllowHybridFlow { get; set; }

    /// <summary>
    /// Gets or sets whether to enable the Device Authorization flow, for TVs and CLI tools.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AllowDeviceAuthorizationFlow { get; set; }

    /// <summary>
    /// Gets or sets whether to enable the <c>none</c> response type, used by clients that only
    /// need to know whether a session exists. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowNoneFlow { get; set; }

    /// <summary>Gets or sets any non-standard grant types to allow.</summary>
    public IList<string> CustomFlows { get; set; } = [];

    /// <summary>
    /// Gets or sets whether PKCE is required for the Authorization Code flow.
    /// Defaults to <c>true</c>, and there is rarely a good reason to lower it.
    /// </summary>
    public bool RequirePkce { get; set; } = true;

    /// <summary>
    /// Gets or sets whether clients must push authorization requests to the PAR endpoint first.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool RequirePushedAuthorizationRequests { get; set; }

    // ── Token lifetimes ──────────────────────────────────────────────────

    /// <summary>Gets or sets the access token lifetime. Defaults to 30 minutes.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Gets or sets the refresh token lifetime. Defaults to 14 days.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Gets or sets the identity token lifetime. Defaults to 20 minutes.</summary>
    public TimeSpan IdentityTokenLifetime { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>Gets or sets the authorization code lifetime. Defaults to 5 minutes.</summary>
    public TimeSpan AuthorizationCodeLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the device code lifetime. Defaults to 10 minutes.</summary>
    public TimeSpan DeviceCodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the user code lifetime. Defaults to 15 minutes.</summary>
    public TimeSpan UserCodeLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets how long a just-redeemed refresh token keeps working, to absorb the races a
    /// client with concurrent requests will produce. Leave <c>null</c> for the OpenIddict default.
    /// Ignored when <see cref="DisableRollingRefreshTokens"/> is <c>true</c>.
    /// </summary>
    public TimeSpan? RefreshTokenReuseLeeway { get; set; }

    // ── Token format and storage ─────────────────────────────────────────

    /// <summary>
    /// Gets or sets whether to stop encrypting access tokens, so they are plain signed JWTs.
    /// Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Turn this on when a resource server validates tokens locally with standard JWT middleware:
    /// an encrypted token is opaque to it, which is the usual cause of "IDX10609: the token has no
    /// payload" at the API. It also makes every access token readable by anything that intercepts
    /// one, so keep claims in them to a minimum.
    /// </remarks>
    public bool DisableAccessTokenEncryption { get; set; }

    /// <summary>
    /// Gets or sets whether access tokens are opaque references stored server-side instead of
    /// self-contained JWTs. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Reference tokens can be revoked instantly and leak nothing, at the cost of a database
    /// lookup on every API call - so resource servers must validate by introspection, not locally.
    /// </remarks>
    public bool UseReferenceAccessTokens { get; set; }

    /// <summary>Gets or sets whether refresh tokens are opaque references. Defaults to <c>false</c>.</summary>
    public bool UseReferenceRefreshTokens { get; set; }

    /// <summary>
    /// Gets or sets whether to stop rotating refresh tokens on use. Defaults to <c>false</c>
    /// (rotation on, which is what lets the server detect a stolen token being replayed).
    /// </summary>
    public bool DisableRollingRefreshTokens { get; set; }

    /// <summary>
    /// Gets or sets whether to stop extending a refresh token lifetime each time it is used.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool DisableSlidingRefreshTokenExpiration { get; set; }

    /// <summary>
    /// Gets or sets whether to stop persisting tokens. Defaults to <c>false</c>.
    /// Turning this on makes revocation and introspection of issued tokens impossible.
    /// </summary>
    public bool DisableTokenStorage { get; set; }

    /// <summary>
    /// Gets or sets whether to stop persisting authorizations. Defaults to <c>false</c>.
    /// Turning this on removes the ability to list or revoke a user's granted consents.
    /// </summary>
    public bool DisableAuthorizationStorage { get; set; }

    // ── Validation and permissions ───────────────────────────────────────

    /// <summary>Gets or sets whether to skip scope validation. Defaults to <c>false</c>.</summary>
    public bool DisableScopeValidation { get; set; }

    /// <summary>Gets or sets whether to skip audience validation. Defaults to <c>false</c>.</summary>
    public bool DisableAudienceValidation { get; set; }

    /// <summary>Gets or sets whether to skip per-client endpoint permissions. Defaults to <c>false</c>.</summary>
    public bool IgnoreEndpointPermissions { get; set; }

    /// <summary>Gets or sets whether to skip per-client grant type permissions. Defaults to <c>false</c>.</summary>
    public bool IgnoreGrantTypePermissions { get; set; }

    /// <summary>Gets or sets whether to skip per-client scope permissions. Defaults to <c>false</c>.</summary>
    public bool IgnoreScopePermissions { get; set; }

    /// <summary>Gets or sets whether to skip per-client response type permissions. Defaults to <c>false</c>.</summary>
    public bool IgnoreResponseTypePermissions { get; set; }

    // ── Scopes ───────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets scopes to register beyond the built-in <c>openid</c>, <c>email</c>,
    /// <c>profile</c>, <c>phone</c>, <c>roles</c> and <c>offline_access</c>.
    /// </summary>
    public IList<string> AdditionalScopes { get; set; } = [];

    // ── Certificates ─────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets whether to use OpenIddict development certificates.
    /// </summary>
    /// <remarks>
    /// Development certificates live in the user certificate store of the machine that created
    /// them, so every instance of a scaled-out deployment ends up with a different key and tokens
    /// stop validating across instances. Startup fails if this is combined with explicitly
    /// configured certificates.
    /// </remarks>
    public bool UseDevelopmentKeys { get; set; }

    /// <summary>
    /// Gets or sets whether to use in-memory ephemeral keys, regenerated on every start.
    /// Suitable only for tests: a restart invalidates every token already issued.
    /// </summary>
    public bool UseEphemeralKeys { get; set; }

    /// <summary>Gets the signing certificate source.</summary>
    public KyrolusOpenIddictCertificateOptions SigningCertificate { get; } = new();

    /// <summary>
    /// Gets the encryption certificate source. Leave it unconfigured to reuse the signing
    /// certificate, which is common but means one compromised key breaks both properties.
    /// </summary>
    public KyrolusOpenIddictCertificateOptions EncryptionCertificate { get; } = new();

    // ── Hosting ──────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets whether the authorization endpoint hands the request to the application
    /// pipeline rather than answering it itself. Defaults to <c>true</c>, which is what
    /// <c>MapKyrolusOpenIddictEndpoints</c> relies on.
    /// </summary>
    public bool EnableAuthorizationEndpointPassthrough { get; set; } = true;

    /// <summary>Gets or sets whether the token endpoint passes through. Defaults to <c>true</c>.</summary>
    public bool EnableTokenEndpointPassthrough { get; set; } = true;

    /// <summary>Gets or sets whether the userinfo endpoint passes through. Defaults to <c>true</c>.</summary>
    public bool EnableUserInfoEndpointPassthrough { get; set; } = true;

    /// <summary>Gets or sets whether the end-session endpoint passes through. Defaults to <c>true</c>.</summary>
    public bool EnableEndSessionEndpointPassthrough { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the end-user verification endpoint passes through.
    /// Defaults to <c>true</c>; only relevant with the device flow.
    /// </summary>
    public bool EnableEndUserVerificationEndpointPassthrough { get; set; } = true;

    /// <summary>
    /// Gets or sets whether protocol errors are surfaced to the application pipeline instead of
    /// being written directly. Defaults to <c>false</c>.
    /// </summary>
    public bool EnableErrorPassthrough { get; set; }

    /// <summary>
    /// Gets or sets whether to accept requests over plain HTTP. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Only ever set this in development. Over HTTP, every token in a request or response is
    /// readable by anything on the path.
    /// </remarks>
    public bool DisableTransportSecurityRequirement { get; set; }

    /// <summary>
    /// Gets or sets whether the authorization server also validates the tokens it issues, so its
    /// own <c>[Authorize]</c> endpoints work. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Without this the default authentication scheme points at a validation handler that was
    /// never registered, and every authenticated request fails at runtime with "No authentication
    /// handler is registered for the scheme 'OpenIddict.Validation.AspNetCore'".
    /// </remarks>
    public bool RegisterLocalValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether OpenIddict validation becomes the default authentication scheme.
    /// Defaults to <c>true</c>. Ignored when <see cref="RegisterLocalValidation"/> is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Set this to <c>false</c> on a server that also signs users in interactively with cookies
    /// and wants the cookie scheme to stay the default; the endpoints mapped by
    /// <c>MapKyrolusOpenIddictEndpoints</c> name their schemes explicitly either way.
    /// </remarks>
    public bool SetValidationAsDefaultScheme { get; set; } = true;

    /// <summary>
    /// Gets or sets whether error responses carry extra Kyrolus diagnostic parameters alongside
    /// the standard OAuth <c>error</c> / <c>error_description</c> fields. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// The standard fields are never replaced - a spec-compliant client keeps working, and one
    /// that understands the extras gets a field-level breakdown it can bind to a form.
    /// </remarks>
    public bool EnrichErrorResponses { get; set; }
}
