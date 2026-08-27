using KyrolusSous.Auth.OpenIddict.Handlers;
using KyrolusSous.Auth.OpenIddict.Options;
using KyrolusSous.Auth.Runtime;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace KyrolusSous.Auth.OpenIddict.Config;

/// <summary>
/// Extension methods for configuring Kyrolus OpenIddict authentication.
/// </summary>
/// <remarks>
/// Storage-agnostic: the application configures its own OpenIddict Core store (EF Core, Marten,
/// MongoDB, Dapper, ...) before calling these methods. Nothing here references an ORM.
/// </remarks>
public static class OpenIddictConfigExtensions
{
    /// <summary>
    /// Adds and configures the Kyrolus OpenIddict authorization server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the authorization server options.</param>
    /// <param name="configureServer">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="OpenIddictServerBuilder"/>.
    /// </param>
    /// <example>
    /// <code>
    /// // 1. The application owns its storage:
    /// services.AddDbContext&lt;MyDbContext&gt;(o => o.UseNpgsql(cs).UseOpenIddict());
    /// services.AddOpenIddict().AddCore(core =>
    ///     core.UseEntityFrameworkCore().UseDbContext&lt;MyDbContext&gt;());
    ///
    /// // 2. Kyrolus configures the protocol:
    /// services.AddKyrolusOpenIddictAuthServer(options =>
    /// {
    ///     options.Issuer = "https://auth.contoso.com";
    ///     options.SigningCertificate.FilePath = "/run/secrets/signing.pfx";
    ///     options.SigningCertificate.Password = configuration["Auth:SigningPassword"];
    ///     options.DisableAccessTokenEncryption = true;   // APIs validate JWTs locally
    /// });
    ///
    /// // 3. The application implements one interface, over any store it likes:
    /// services.AddKyrolusAuthUserStore&lt;MyUserStore&gt;();
    ///
    /// // 4. And gets the protocol endpoints for free:
    /// app.MapKyrolusOpenIddictEndpoints();
    /// </code>
    /// </example>
    public static IServiceCollection AddKyrolusOpenIddictAuthServer(
        this IServiceCollection services,
        Action<KyrolusOpenIddictOptions> configure,
        Action<OpenIddictServerBuilder>? configureServer = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusOpenIddictOptions();
        configure(options);

        KyrolusOpenIddictOptionsValidator.Validate(options);

        // The endpoints resolve these at request time; the server builder consumes them now.
        services.AddSingleton(options);
        services.AddKyrolusAuthCore();

        services.AddOpenIddict().AddServer(server =>
        {
            ConfigureEndpoints(server, options);
            ConfigureFlows(server, options);
            ConfigureScopes(server, options);
            ConfigureCredentials(server, options);
            ConfigureLifetimes(server, options);
            ConfigureTokenBehaviour(server, options);
            ConfigureAspNetCore(server, options);

            if (options.EnrichErrorResponses)
            {
                // Ordered just before the ASP.NET Core integration turns the response into JSON.
                // Running any later (int.MaxValue, say) mutates a payload that has already been
                // written to the wire, so the extra parameters never reach the client.
                server.AddEventHandler<ApplyTokenResponseContext>(builder => builder
                    .UseSingletonHandler<KyrolusErrorEnrichmentHandler>()
                    .SetOrder(OpenIddictServerAspNetCoreHandlers
                        .AttachHttpResponseCode<ApplyTokenResponseContext>.Descriptor.Order - 1));
            }

            configureServer?.Invoke(server);
        });

        if (options.RegisterLocalValidation)
        {
            services.AddOpenIddict().AddValidation(validation =>
            {
                // Reads the server configuration in-process: no HTTP round trip, and no risk of
                // the two halves drifting apart on key rotation.
                validation.UseLocalServer();
                validation.UseAspNetCore();
            });

            if (options.SetValidationAsDefaultScheme)
            {
                services.AddAuthentication(auth =>
                {
                    auth.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                    auth.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                    auth.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                });
            }
        }

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Adds and configures a resource server (API) that accepts tokens issued by a Kyrolus
    /// OpenIddict authorization server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the resource server options.</param>
    /// <param name="configureValidation">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="OpenIddictValidationBuilder"/>.
    /// </param>
    /// <example>
    /// <code>
    /// services.AddKyrolusOpenIddictApiServer(options =>
    /// {
    ///     options.Issuer = "https://auth.contoso.com";
    ///     options.Audiences.Add("orders-api");
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddKyrolusOpenIddictApiServer(
        this IServiceCollection services,
        Action<KyrolusOpenIddictApiOptions> configure,
        Action<OpenIddictValidationBuilder>? configureValidation = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusOpenIddictApiOptions();
        configure(options);

        KyrolusOpenIddictOptionsValidator.Validate(options);

        services.AddOpenIddict().AddValidation(validation =>
        {
            validation.SetIssuer(options.Issuer);

            foreach (var audience in options.Audiences)
            {
                if (!string.IsNullOrWhiteSpace(audience))
                {
                    validation.AddAudiences(audience);
                }
            }

            if (options.ValidationMode == KyrolusTokenValidationMode.Introspection)
            {
                validation.UseIntrospection()
                          .SetClientId(options.ClientId!)
                          .SetClientSecret(options.ClientSecret!);
            }
            else if (KyrolusCertificateResolver.Resolve(options.EncryptionCertificate, "encryption") is { } certificate)
            {
                // Only needed when the authorization server encrypts its access tokens; without
                // the matching key this API cannot read them at all.
                validation.AddEncryptionCertificate(certificate);
            }

            // Needed for discovery of the issuer configuration, and for introspection calls.
            validation.UseSystemNetHttp();
            validation.UseAspNetCore();

            configureValidation?.Invoke(validation);
        });

        if (options.SetAsDefaultScheme)
        {
            services.AddAuthentication(auth =>
            {
                auth.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                auth.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                auth.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });
        }

        services.AddAuthorization();

        return services;
    }

    private static void ConfigureEndpoints(OpenIddictServerBuilder server, KyrolusOpenIddictOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Issuer))
        {
            server.SetIssuer(options.Issuer);
        }

        server.SetTokenEndpointUris(options.TokenEndpoint);
        server.SetIntrospectionEndpointUris(options.IntrospectionEndpoint);
        server.SetRevocationEndpointUris(options.RevocationEndpoint);
        server.SetUserInfoEndpointUris(options.UserInfoEndpoint);
        server.SetEndSessionEndpointUris(options.EndSessionEndpoint);

        // An authorization endpoint with no interactive flow enabled would advertise a route that
        // can only ever return an error, so it is registered with the flows that need it.
        if (options.AllowAuthorizationCodeFlow || options.AllowImplicitFlow ||
            options.AllowHybridFlow || options.AllowNoneFlow)
        {
            server.SetAuthorizationEndpointUris(options.AuthorizationEndpoint);
        }

        if (options.AllowDeviceAuthorizationFlow)
        {
            server.SetDeviceAuthorizationEndpointUris(options.DeviceAuthorizationEndpoint);
            server.SetEndUserVerificationEndpointUris(options.EndUserVerificationEndpoint);
        }
    }

    private static void ConfigureFlows(OpenIddictServerBuilder server, KyrolusOpenIddictOptions options)
    {
        if (options.AllowAuthorizationCodeFlow)
        {
            server.AllowAuthorizationCodeFlow();

            if (options.RequirePkce)
            {
                server.RequireProofKeyForCodeExchange();
            }
        }

        if (options.AllowRefreshTokenFlow)
        {
            server.AllowRefreshTokenFlow();
        }

        if (options.AllowClientCredentialsFlow)
        {
            server.AllowClientCredentialsFlow();
        }

        if (options.AllowPasswordFlow)
        {
            server.AllowPasswordFlow();
        }

        if (options.AllowImplicitFlow)
        {
            server.AllowImplicitFlow();
        }

        if (options.AllowHybridFlow)
        {
            server.AllowHybridFlow();
        }

        if (options.AllowDeviceAuthorizationFlow)
        {
            server.AllowDeviceAuthorizationFlow();
        }

        if (options.AllowNoneFlow)
        {
            server.AllowNoneFlow();
        }

        foreach (var flow in options.CustomFlows)
        {
            if (!string.IsNullOrWhiteSpace(flow))
            {
                server.AllowCustomFlow(flow);
            }
        }

        if (options.RequirePushedAuthorizationRequests)
        {
            server.SetPushedAuthorizationEndpointUris("/connect/par");
            server.RequirePushedAuthorizationRequests();
        }
    }

    private static void ConfigureScopes(OpenIddictServerBuilder server, KyrolusOpenIddictOptions options)
    {
        // These are exactly the scopes the Kyrolus claims-principal factory and destination rules
        // understand. Registering fewer would make a client asking for one of them fail with
        // invalid_scope even though the server knows how to honour it.
        var scopes = new List<string>(6 + options.AdditionalScopes.Count)
        {
            Scopes.OpenId,
            Scopes.Email,
            Scopes.Profile,
            Scopes.Phone,
            Scopes.Roles,
            Scopes.OfflineAccess,
        };

        foreach (var scope in options.AdditionalScopes)
        {
            if (!string.IsNullOrWhiteSpace(scope) && !scopes.Contains(scope))
            {
                scopes.Add(scope);
            }
        }

        server.RegisterScopes([.. scopes]);
    }

    private static void ConfigureCredentials(OpenIddictServerBuilder server, KyrolusOpenIddictOptions options)
    {
        if (options.UseDevelopmentKeys)
        {
            server.AddDevelopmentEncryptionCertificate()
                  .AddDevelopmentSigningCertificate();
            return;
        }

        if (options.UseEphemeralKeys)
        {
            server.AddEphemeralEncryptionKey()
                  .AddEphemeralSigningKey();
            return;
        }

        var signing = KyrolusCertificateResolver.Resolve(options.SigningCertificate, "signing");
        if (signing is not null)
        {
            server.AddSigningCertificate(signing);
        }

        var encryption = KyrolusCertificateResolver.Resolve(options.EncryptionCertificate, "encryption");
        if (encryption is not null)
        {
            server.AddEncryptionCertificate(encryption);
        }
        else if (signing is not null && !options.DisableAccessTokenEncryption)
        {
            // Falling back to the signing certificate keeps a single-certificate deployment
            // working. It is not ideal - one compromised key then breaks both signing and
            // encryption - which is why the validator warns about it in the XML docs.
            server.AddEncryptionCertificate(signing);
        }
    }

    private static void ConfigureLifetimes(OpenIddictServerBuilder server, KyrolusOpenIddictOptions options)
    {
        server.SetAccessTokenLifetime(options.AccessTokenLifetime);
        server.SetRefreshTokenLifetime(options.RefreshTokenLifetime);
        server.SetIdentityTokenLifetime(options.IdentityTokenLifetime);
        server.SetAuthorizationCodeLifetime(options.AuthorizationCodeLifetime);

        if (options.AllowDeviceAuthorizationFlow)
        {
            server.SetDeviceCodeLifetime(options.DeviceCodeLifetime);
            server.SetUserCodeLifetime(options.UserCodeLifetime);
        }

        if (options.RefreshTokenReuseLeeway is { } leeway && !options.DisableRollingRefreshTokens)
        {
            server.SetRefreshTokenReuseLeeway(leeway);
        }
    }

    private static void ConfigureTokenBehaviour(OpenIddictServerBuilder server, KyrolusOpenIddictOptions options)
    {
        if (options.DisableAccessTokenEncryption)
        {
            server.DisableAccessTokenEncryption();
        }

        if (options.UseReferenceAccessTokens)
        {
            server.UseReferenceAccessTokens();
        }

        if (options.UseReferenceRefreshTokens)
        {
            server.UseReferenceRefreshTokens();
        }

        if (options.DisableRollingRefreshTokens)
        {
            server.DisableRollingRefreshTokens();
        }

        if (options.DisableSlidingRefreshTokenExpiration)
        {
            server.DisableSlidingRefreshTokenExpiration();
        }

        if (options.DisableTokenStorage)
        {
            server.DisableTokenStorage();
        }

        if (options.DisableAuthorizationStorage)
        {
            server.DisableAuthorizationStorage();
        }

        if (options.DisableScopeValidation)
        {
            server.DisableScopeValidation();
        }

        if (options.DisableAudienceValidation)
        {
            server.DisableAudienceValidation();
        }

        if (options.IgnoreEndpointPermissions)
        {
            server.IgnoreEndpointPermissions();
        }

        if (options.IgnoreGrantTypePermissions)
        {
            server.IgnoreGrantTypePermissions();
        }

        if (options.IgnoreScopePermissions)
        {
            server.IgnoreScopePermissions();
        }

        if (options.IgnoreResponseTypePermissions)
        {
            server.IgnoreResponseTypePermissions();
        }
    }

    private static void ConfigureAspNetCore(OpenIddictServerBuilder server, KyrolusOpenIddictOptions options)
    {
        var aspNetCore = server.UseAspNetCore();

        if (options.EnableAuthorizationEndpointPassthrough &&
            (options.AllowAuthorizationCodeFlow || options.AllowImplicitFlow ||
             options.AllowHybridFlow || options.AllowNoneFlow))
        {
            aspNetCore.EnableAuthorizationEndpointPassthrough();
        }

        if (options.EnableTokenEndpointPassthrough)
        {
            aspNetCore.EnableTokenEndpointPassthrough();
        }

        if (options.EnableUserInfoEndpointPassthrough)
        {
            aspNetCore.EnableUserInfoEndpointPassthrough();
        }

        if (options.EnableEndSessionEndpointPassthrough)
        {
            aspNetCore.EnableEndSessionEndpointPassthrough();
        }

        if (options.EnableEndUserVerificationEndpointPassthrough && options.AllowDeviceAuthorizationFlow)
        {
            aspNetCore.EnableEndUserVerificationEndpointPassthrough();
        }

        if (options.EnableErrorPassthrough)
        {
            aspNetCore.EnableErrorPassthrough();
        }

        if (options.DisableTransportSecurityRequirement)
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    }
}
