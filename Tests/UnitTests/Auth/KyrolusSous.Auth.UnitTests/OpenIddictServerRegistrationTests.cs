using KyrolusSous.Auth.OpenIddict.Config;
using KyrolusSous.Auth.OpenIddict.Options;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

public sealed class OpenIddictServerRegistrationTests
{
    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static string RegistrationFailure(Action<KyrolusOpenIddictOptions> configure)
        => Should.Throw<InvalidOperationException>(
            () => NewServices().AddKyrolusOpenIddictAuthServer(configure)).Message;

    [Fact(DisplayName = "A development configuration registers")]
    public void A_development_configuration_registers()
    {
        var services = NewServices();

        services.AddKyrolusOpenIddictAuthServer(o => o.UseDevelopmentKeys = true);

        services.BuildServiceProvider()
            .GetRequiredService<KyrolusOpenIddictOptions>()
            .UseDevelopmentKeys.ShouldBeTrue();
    }

    [Fact(DisplayName = "A server with no signing key fails at startup")]
    public void A_server_with_no_signing_key_fails_at_startup()
    {
        RegistrationFailure(_ => { }).ShouldContain("No signing key is configured");
    }

    [Fact(DisplayName = "Development keys alongside a real certificate fail at startup")]
    public void Development_keys_alongside_a_real_certificate_fail_at_startup()
    {
        // The generated key would silently win, and the certificate that was configured on
        // purpose would never be used.
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.SigningCertificate.FilePath = "/certs/signing.pfx";
        }).ShouldContain("Pick one");
    }

    [Fact(DisplayName = "Development and ephemeral keys together fail at startup")]
    public void Development_and_ephemeral_keys_together_fail_at_startup()
    {
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.UseEphemeralKeys = true;
        }).ShouldContain("cannot both be enabled");
    }

    [Fact(DisplayName = "A server with no grant type fails at startup")]
    public void A_server_with_no_grant_type_fails_at_startup()
    {
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.AllowAuthorizationCodeFlow = false;
            o.AllowRefreshTokenFlow = false;
        }).ShouldContain("No grant type is enabled");
    }

    [Fact(DisplayName = "A refresh flow with nothing to refresh fails at startup")]
    public void A_refresh_flow_with_nothing_to_refresh_fails_at_startup()
    {
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.AllowAuthorizationCodeFlow = false;
            o.AllowClientCredentialsFlow = true;
            o.AllowRefreshTokenFlow = true;
        }).ShouldContain("no flow that can issue a refresh token");
    }

    [Fact(DisplayName = "A refresh token shorter than the access token fails at startup")]
    public void A_refresh_token_shorter_than_the_access_token_fails_at_startup()
    {
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.AccessTokenLifetime = TimeSpan.FromHours(2);
            o.RefreshTokenLifetime = TimeSpan.FromMinutes(30);
        }).ShouldContain("must be longer than");
    }

    [Theory(DisplayName = "A relative endpoint path fails at startup")]
    [InlineData("connect/token")]
    [InlineData("")]
    public void A_relative_endpoint_path_fails_at_startup(string path)
    {
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.TokenEndpoint = path;
        }).ShouldContain(nameof(KyrolusOpenIddictOptions.TokenEndpoint));
    }

    [Fact(DisplayName = "A non absolute issuer fails at startup")]
    public void A_non_absolute_issuer_fails_at_startup()
    {
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.Issuer = "auth.contoso.com";
        }).ShouldContain("absolute URI");
    }

    [Fact(DisplayName = "Reference tokens without token storage fail at startup")]
    public void Reference_tokens_without_token_storage_fail_at_startup()
    {
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.UseReferenceAccessTokens = true;
            o.DisableTokenStorage = true;
        }).ShouldContain("needs token storage");
    }

    [Fact(DisplayName = "A zero lifetime fails at startup")]
    public void A_zero_lifetime_fails_at_startup()
    {
        RegistrationFailure(o =>
        {
            o.UseDevelopmentKeys = true;
            o.AccessTokenLifetime = TimeSpan.Zero;
        }).ShouldContain(nameof(KyrolusOpenIddictOptions.AccessTokenLifetime));
    }

    [Fact(DisplayName = "The auth runtime defaults are registered alongside the server")]
    public void The_auth_runtime_defaults_are_registered_alongside_the_server()
    {
        var services = NewServices();

        services.AddKyrolusOpenIddictAuthServer(o => o.UseDevelopmentKeys = true);

        var descriptors = services.Select(d => d.ServiceType.Name).ToList();

        descriptors.ShouldContain(nameof(Abstractions.IKyrolusPasswordHasher));
        descriptors.ShouldContain(nameof(Abstractions.IKyrolusClaimsPrincipalFactory));
        descriptors.ShouldContain(nameof(Abstractions.IKyrolusUserAuthenticator));

        // The user store is deliberately not registered: supplying one is what keeps these
        // packages free of any storage dependency.
        descriptors.ShouldNotContain(nameof(Abstractions.IKyrolusAuthUserStore));
    }

    // ── Resource server ──────────────────────────────────────────────────

    [Fact(DisplayName = "An api server with no issuer fails at startup")]
    public void An_api_server_with_no_issuer_fails_at_startup()
    {
        Should.Throw<InvalidOperationException>(
                () => NewServices().AddKyrolusOpenIddictApiServer(_ => { }))
            .Message.ShouldContain(nameof(KyrolusOpenIddictApiOptions.Issuer));
    }

    [Fact(DisplayName = "An api server using introspection needs client credentials")]
    public void An_api_server_using_introspection_needs_client_credentials()
    {
        var message = Should.Throw<InvalidOperationException>(
            () => NewServices().AddKyrolusOpenIddictApiServer(o =>
            {
                o.Issuer = "https://auth.contoso.com";
                o.ValidationMode = KyrolusTokenValidationMode.Introspection;
            })).Message;

        message.ShouldContain(nameof(KyrolusOpenIddictApiOptions.ClientId));
        message.ShouldContain(nameof(KyrolusOpenIddictApiOptions.ClientSecret));
    }

    [Fact(DisplayName = "An api server validating locally registers")]
    public void An_api_server_validating_locally_registers()
    {
        var services = NewServices();

        services.AddKyrolusOpenIddictApiServer(o =>
        {
            o.Issuer = "https://auth.contoso.com";
            o.Audiences.Add("orders-api");
        });

        // No JwtBearer handler is added: mixing one in alongside OpenIddict validation was how
        // the previous version ended up validating tokens with ValidateIssuerSigningKey = false.
        services.ShouldNotContain(d => d.ServiceType.FullName!.Contains("JwtBearer", StringComparison.Ordinal));
    }
}
