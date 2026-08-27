using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.OpenIddict.Options;

/// <summary>
/// Options for the ready-made OpenIddict protocol endpoints mapped by
/// <c>MapKyrolusOpenIddictEndpoints</c>.
/// </summary>
public sealed class KyrolusOpenIddictEndpointOptions
{
    /// <summary>Gets or sets whether to map the token endpoint. Defaults to <c>true</c>.</summary>
    public bool MapTokenEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to map the authorization endpoint. Defaults to <c>true</c>.
    /// Ignored when no interactive flow is enabled.
    /// </summary>
    public bool MapAuthorizationEndpoint { get; set; } = true;

    /// <summary>Gets or sets whether to map the userinfo endpoint. Defaults to <c>true</c>.</summary>
    public bool MapUserInfoEndpoint { get; set; } = true;

    /// <summary>Gets or sets whether to map the end-session (logout) endpoint. Defaults to <c>true</c>.</summary>
    public bool MapEndSessionEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets the scheme that carries the interactive sign-in session the authorization
    /// endpoint reads. Defaults to the cookie scheme.
    /// </summary>
    public string InteractiveAuthenticationScheme { get; set; }
        = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Gets or sets where the user goes after an end-session request that names no
    /// <c>post_logout_redirect_uri</c>. Defaults to <c>"/"</c>.
    /// </summary>
    public string PostLogoutRedirectUri { get; set; } = "/";

    /// <summary>
    /// Gets or sets fixed audiences stamped on every issued principal, on top of the resources
    /// derived from the granted scopes.
    /// </summary>
    public IList<string> Resources { get; set; } = [];

    /// <summary>
    /// Gets or sets the rule deciding which token each claim lands in. Leave <c>null</c> for
    /// <see cref="KyrolusClaimDestinations.GetDestinations(Claim, ClaimsPrincipal)"/>.
    /// </summary>
    public Func<Claim, ClaimsPrincipal, IEnumerable<string>>? ClaimDestinationResolver { get; set; }

    /// <summary>
    /// Gets or sets a hook run just before a principal is signed in, for claims that need the
    /// request (tenant resolved from the host, an impersonation marker, an audit id).
    /// </summary>
    /// <remarks>
    /// Claims added here still need destinations. Adding them before sign-in means
    /// <see cref="ClaimDestinationResolver"/> has already run over the rest of the principal, so
    /// set the destination on the new claim explicitly.
    /// </remarks>
    public Func<ClaimsPrincipal, KyrolusAuthUser?, HttpContext, ValueTask>? OnPrincipalCreated { get; set; }

    /// <summary>
    /// Gets or sets whether the authorization endpoint returns <c>login_required</c> instead of
    /// challenging when the user has no interactive session. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Set this on an authorization server that hosts no login UI of its own - a headless server
    /// challenging a scheme with nowhere to redirect produces a confusing loop.
    /// </remarks>
    public bool RequireExistingSession { get; set; }
}
