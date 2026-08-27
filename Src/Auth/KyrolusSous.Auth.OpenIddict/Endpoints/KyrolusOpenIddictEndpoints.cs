using System.Collections.Immutable;
using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.OpenIddict.Options;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

// Deliberately in the root namespace: a nested "Endpoints" namespace risks shadowing the
// OpenIddict constant classes this file leans on, the same way a "Claims" namespace would.
namespace KyrolusSous.Auth.OpenIddict;

/// <summary>
/// Maps the OpenIddict protocol endpoints - token, authorize, userinfo and end-session - onto the
/// storage-agnostic Kyrolus auth abstractions.
/// </summary>
/// <remarks>
/// <para>
/// These four handlers are the boilerplate every OpenIddict deployment writes by hand, and where
/// most of its bugs live: forgetting claim destinations, not re-reading the user on refresh, or
/// signing in a principal that carries the wrong scopes. Mapping them from a library means one
/// audited copy instead of one per application.
/// </para>
/// <para>
/// The only thing an application has to supply is an <see cref="IKyrolusAuthUserStore"/>, so
/// nothing here is tied to an ORM or a database.
/// </para>
/// <para>
/// Consent is implicit: any client that reaches the authorization endpoint with a signed-in user
/// is granted the scopes it asked for. That is right for first-party clients and wrong for
/// third-party ones - a server that federates to outside clients should map its own authorization
/// endpoint with a consent screen instead of enabling this one.
/// </para>
/// </remarks>
public static class KyrolusOpenIddictEndpoints
{
    /// <summary>
    /// Maps the OpenIddict protocol endpoints at the paths configured on
    /// <see cref="KyrolusOpenIddictOptions"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optionally configures which endpoints are mapped and how they behave.</param>
    /// <example>
    /// <code>
    /// app.MapKyrolusOpenIddictEndpoints(options =>
    /// {
    ///     options.Resources.Add("orders-api");
    /// });
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapKyrolusOpenIddictEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<KyrolusOpenIddictEndpointOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var serverOptions = endpoints.ServiceProvider.GetService<KyrolusOpenIddictOptions>()
            ?? throw new InvalidOperationException(
                $"{nameof(MapKyrolusOpenIddictEndpoints)} requires {nameof(KyrolusOpenIddictOptions)} in the " +
                "service provider. Call AddKyrolusOpenIddictAuthServer during service registration first.");

        var options = new KyrolusOpenIddictEndpointOptions();
        configure?.Invoke(options);

        if (options.MapTokenEndpoint)
        {
            endpoints.MapMethods(
                    serverOptions.TokenEndpoint,
                    ["POST"],
                    Execute(context => HandleTokenAsync(context, options)))
                .AllowAnonymous()
                // OAuth clients post form-encoded bodies and have no antiforgery token to send.
                .DisableAntiforgery()
                .WithName("KyrolusOpenIddictToken");
        }

        var interactiveFlowEnabled = serverOptions.AllowAuthorizationCodeFlow ||
                                     serverOptions.AllowImplicitFlow ||
                                     serverOptions.AllowHybridFlow ||
                                     serverOptions.AllowNoneFlow;

        if (options.MapAuthorizationEndpoint && interactiveFlowEnabled)
        {
            endpoints.MapMethods(
                    serverOptions.AuthorizationEndpoint,
                    ["GET", "POST"],
                    Execute(context => HandleAuthorizeAsync(context, options)))
                .AllowAnonymous()
                .DisableAntiforgery()
                .WithName("KyrolusOpenIddictAuthorize");
        }

        if (options.MapUserInfoEndpoint)
        {
            endpoints.MapMethods(
                    serverOptions.UserInfoEndpoint,
                    ["GET", "POST"],
                    Execute(context => HandleUserInfoAsync(context)))
                .AllowAnonymous()
                .DisableAntiforgery()
                .WithName("KyrolusOpenIddictUserInfo");
        }

        if (options.MapEndSessionEndpoint)
        {
            endpoints.MapMethods(
                    serverOptions.EndSessionEndpoint,
                    ["GET", "POST"],
                    Execute(context => HandleEndSessionAsync(context, options)))
                .AllowAnonymous()
                .DisableAntiforgery()
                .WithName("KyrolusOpenIddictEndSession");
        }

        return endpoints;
    }

    /// <summary>
    /// Wraps a handler as a <see cref="RequestDelegate"/>.
    /// </summary>
    /// <remarks>
    /// The <c>Map*(..., Delegate)</c> overloads build their request delegate by reflecting over the
    /// handler signature, which trims badly and is flagged by the AOT analyzer. A library cannot
    /// rely on the request-delegate source generator either - that only runs over the Map
    /// calls in the application itself. Executing the <see cref="IResult"/> by hand keeps this package
    /// AOT-clean.
    /// </remarks>
    private static RequestDelegate Execute(Func<HttpContext, Task<IResult>> handler)
        => async context =>
        {
            var result = await handler(context).ConfigureAwait(false);
            await result.ExecuteAsync(context).ConfigureAwait(false);
        };

    // ── Token endpoint ───────────────────────────────────────────────────────────────────

    private static async Task<IResult> HandleTokenAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options)
    {
        var request = GetRequest(context);

        if (request.IsPasswordGrantType())
        {
            var serverOptions = context.RequestServices.GetService<KyrolusOpenIddictOptions>();
            if (serverOptions is not null && !serverOptions.AllowPasswordFlow)
            {
                return Forbid(
                    Errors.UnsupportedGrantType,
                    "The resource owner password grant is disabled on this server.");
            }

            return await HandlePasswordGrantAsync(context, options, request).ConfigureAwait(false);
        }

        if (request.IsAuthorizationCodeGrantType() ||
            request.IsRefreshTokenGrantType() ||
            request.IsDeviceCodeGrantType())
        {
            return await HandleStoredGrantAsync(context, options, request).ConfigureAwait(false);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return await HandleClientCredentialsAsync(context, options, request).ConfigureAwait(false);
        }

        return Forbid(
            Errors.UnsupportedGrantType,
            $"The '{request.GrantType}' grant type is not supported by this server.");
    }

    private static async Task<IResult> HandlePasswordGrantAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options,
        OpenIddictRequest request)
    {
        var authenticator = context.RequestServices.GetRequiredService<IKyrolusUserAuthenticator>();

        var result = await authenticator
            .AuthenticateAsync(request.Username ?? "", request.Password ?? "", context.RequestAborted)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            // invalid_grant is the code RFC 6749 reserves for bad resource-owner credentials.
            // The description is the authenticator's, which is already written to be safe to
            // return: it never distinguishes an unknown user from a wrong password.
            return Forbid(Errors.InvalidGrant, result.ErrorDescription ?? "The credentials are invalid.");
        }

        var scopes = request.GetScopes();
        var principal = await BuildPrincipalAsync(context, options, result.User!, scopes).ConfigureAwait(false);

        return SignIn(principal);
    }

    private static async Task<IResult> HandleStoredGrantAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options,
        OpenIddictRequest request)
    {
        var result = await context
            .AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        if (result.Principal is null)
        {
            return Forbid(Errors.InvalidGrant, "The token is no longer valid.");
        }

        var subject = result.Principal.GetClaim(Claims.Subject);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Forbid(Errors.InvalidGrant, "The token carries no subject.");
        }

        var userStore = context.RequestServices.GetRequiredService<IKyrolusAuthUserStore>();
        var user = await userStore.FindByIdAsync(subject, context.RequestAborted).ConfigureAwait(false);

        // Re-reading the user on every redemption is the whole point: a role revoked, an account
        // disabled or a user deleted takes effect at the next refresh instead of whenever the last
        // long-lived token happens to expire.
        if (user is null)
        {
            return Forbid(Errors.InvalidGrant, "The account no longer exists.");
        }

        if (!user.IsActive)
        {
            return Forbid(Errors.InvalidGrant, "The account is disabled.");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return Forbid(Errors.InvalidGrant, "The account is locked out.");
        }

        // A refresh request may narrow the scopes; OpenIddict has already rejected anything that
        // is not a subset of the original grant. An authorization-code request carries no scope
        // parameter at all, so the scopes come from the stored principal instead.
        var requested = request.GetScopes();
        var scopes = requested.Length > 0 ? requested : result.Principal.GetScopes();

        var principal = await BuildPrincipalAsync(context, options, user, scopes).ConfigureAwait(false);

        return SignIn(principal);
    }

    private static async Task<IResult> HandleClientCredentialsAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options,
        OpenIddictRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return Forbid(Errors.InvalidClient, "The client_id is missing.");
        }

        // No user is involved in this grant, so the client itself is the subject.
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.AddClaim(new Claim(Claims.Subject, request.ClientId));
        identity.AddClaim(new Claim(Claims.Name, request.ClientId));

        var principal = new ClaimsPrincipal(identity);
        var scopes = request.GetScopes()
            .Where(s => !string.Equals(s, Scopes.OpenId, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(s, Scopes.Profile, StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

        await FinalizePrincipalAsync(context, options, principal, user: null, scopes).ConfigureAwait(false);

        return SignIn(principal);
    }

    // ── Authorization endpoint ───────────────────────────────────────────

    private static async Task<IResult> HandleAuthorizeAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options)
    {
        var request = GetRequest(context);

        // AuthenticateAsync throws on an unregistered scheme. A headless authorization server
        // with no cookie handler should answer login_required, not crash with a 500.
        if (!await HasSchemeAsync(context, options.InteractiveAuthenticationScheme).ConfigureAwait(false))
        {
            return Forbid(
                Errors.LoginRequired,
                $"No interactive authentication scheme named '{options.InteractiveAuthenticationScheme}' " +
                "is registered, so no user can be signed in here.");
        }

        var session = await context
            .AuthenticateAsync(options.InteractiveAuthenticationScheme)
            .ConfigureAwait(false);

        var mustReauthenticate = !session.Succeeded ||
                                 request.HasPromptValue(PromptValues.Login) ||
                                 IsAuthenticationTooOld(request, session);

        if (mustReauthenticate)
        {
            // prompt=none means "do not show me any UI"; the only conforming answer when there is
            // no usable session is the login_required error.
            if (request.HasPromptValue(PromptValues.None) || options.RequireExistingSession)
            {
                return Forbid(Errors.LoginRequired, "The user is not signed in.");
            }

            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = BuildCurrentUrl(context) },
                [options.InteractiveAuthenticationScheme]);
        }

        var subject = session.Principal?.GetClaim(Claims.Subject)
                      ?? session.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Forbid(Errors.LoginRequired, "The sign-in session carries no subject.");
        }

        var userStore = context.RequestServices.GetRequiredService<IKyrolusAuthUserStore>();
        var user = await userStore.FindByIdAsync(subject, context.RequestAborted).ConfigureAwait(false);

        if (user is null || !user.IsActive)
        {
            return Forbid(Errors.LoginRequired, "The account is no longer available.");
        }

        var scopes = request.GetScopes();
        var principal = await BuildPrincipalAsync(context, options, user, scopes).ConfigureAwait(false);

        return SignIn(principal);
    }

    // ── Userinfo endpoint ────────────────────────────────────────────────

    private static async Task<IResult> HandleUserInfoAsync(HttpContext context)
    {
        // The OpenIddict server extracts and validates the access token for its own userinfo
        // endpoint. Reading context.User instead would tie this handler to whichever scheme the
        // application made its default, and break outright when that is not OpenIddict.
        var result = await context
            .AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        var principal = result.Principal ?? context.User;
        var subject = principal.GetClaim(Claims.Subject);

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Results.Challenge(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The access token is not valid.",
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var userStore = context.RequestServices.GetRequiredService<IKyrolusAuthUserStore>();
        var user = await userStore.FindByIdAsync(subject, context.RequestAborted).ConfigureAwait(false);

        if (user is null)
        {
            return Results.Challenge(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The account no longer exists.",
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        if (!user.IsActive)
        {
            return Results.Challenge(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The account has been disabled.",
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        return new KyrolusUserInfoResult(user, principal);
    }

    // ── End-session endpoint ─────────────────────────────────────────────

    private static async Task<IResult> HandleEndSessionAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options)
    {
        // Clearing the interactive session first means a client that ignores the redirect still
        // ends up signed out locally. Skipped when no such scheme is registered - SignOutAsync
        // throws on an unknown scheme, and a headless server has nothing to clear anyway.
        if (await HasSchemeAsync(context, options.InteractiveAuthenticationScheme).ConfigureAwait(false))
        {
            await context.SignOutAsync(options.InteractiveAuthenticationScheme).ConfigureAwait(false);
        }

        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = options.PostLogoutRedirectUri },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    // ── Shared ───────────────────────────────────────────────────────────

    private static async Task<ClaimsPrincipal> BuildPrincipalAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options,
        KyrolusAuthUser user,
        ImmutableArray<string> scopes)
    {
        var factory = context.RequestServices.GetRequiredService<IKyrolusClaimsPrincipalFactory>();

        var principal = await factory
            .CreateAsync(
                user,
                scopes,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                context.RequestAborted)
            .ConfigureAwait(false);

        await FinalizePrincipalAsync(context, options, principal, user, scopes).ConfigureAwait(false);

        return principal;
    }

    private static async Task FinalizePrincipalAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options,
        ClaimsPrincipal principal,
        KyrolusAuthUser? user,
        ImmutableArray<string> scopes)
    {
        principal.SetScopes(scopes);
        await AttachResourcesAsync(context, options, principal, scopes).ConfigureAwait(false);

        if (options.OnPrincipalCreated is not null)
        {
            await options.OnPrincipalCreated(principal, user, context).ConfigureAwait(false);
        }

        // Last, so it also covers whatever OnPrincipalCreated added. A claim with no destination
        // is silently dropped from every token, which is the single most common way a working
        // authorization server ends up issuing empty-looking tokens.
        if (options.ClaimDestinationResolver is { } resolver)
        {
            principal.SetKyrolusDestinations(resolver);
        }
        else
        {
            principal.SetKyrolusDestinations();
        }
    }

    private static async Task AttachResourcesAsync(
        HttpContext context,
        KyrolusOpenIddictEndpointOptions options,
        ClaimsPrincipal principal,
        ImmutableArray<string> scopes)
    {
        var resources = new List<string>(options.Resources);

        // The scope manager only exists once the application has configured OpenIddict Core
        // storage. Treating it as optional keeps this endpoint usable in degraded mode.
        var scopeManager = context.RequestServices.GetService<IOpenIddictScopeManager>();
        if (scopeManager is not null && scopes.Length > 0)
        {
            await foreach (var resource in scopeManager
                               .ListResourcesAsync(scopes, context.RequestAborted)
                               .ConfigureAwait(false))
            {
                if (!resources.Contains(resource))
                {
                    resources.Add(resource);
                }
            }
        }

        if (resources.Count > 0)
        {
            principal.SetResources(resources);
        }
    }

    /// <summary>
    /// Applies the OpenID Connect <c>max_age</c> parameter: a client can insist the user
    /// authenticated within the last N seconds, regardless of how long the session cookie lives.
    /// </summary>
    private static bool IsAuthenticationTooOld(OpenIddictRequest request, AuthenticateResult session)
    {
        if (request.MaxAge is not { } maxAge || session.Properties is null)
        {
            return false;
        }

        if (session.Properties.IssuedUtc is not { } issued)
        {
            // The client asked for a freshness guarantee the session cannot back up. Re-authenticate.
            return true;
        }

        return DateTimeOffset.UtcNow - issued > TimeSpan.FromSeconds(maxAge);
    }

    private static async Task<bool> HasSchemeAsync(HttpContext context, string scheme)
    {
        var provider = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        return await provider.GetSchemeAsync(scheme).ConfigureAwait(false) is not null;
    }

    private static string BuildCurrentUrl(HttpContext context)
        => context.Request.PathBase + context.Request.Path + context.Request.QueryString;

    private static OpenIddictRequest GetRequest(HttpContext context)
        => context.GetOpenIddictServerRequest()
           ?? throw new InvalidOperationException(
               "The OpenIddict request could not be read. This endpoint only works when the matching " +
               "OpenIddict pass-through is enabled - check the Enable*EndpointPassthrough options.");

    private static IResult SignIn(ClaimsPrincipal principal)
        => Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    private static IResult Forbid(string error, string description)
        => Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
