using AspNet.Security.OAuth.GitHub;
using AspNet.Security.OAuth.Twitter;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Apple;
using KyrolusSous.Auth.Discord;
using KyrolusSous.Auth.Facebook;
using KyrolusSous.Auth.GitHub;
using KyrolusSous.Auth.Google;
using KyrolusSous.Auth.LinkedIn;
using KyrolusSous.Auth.MicrosoftAccount;
using KyrolusSous.Auth.X;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

public sealed class ExternalProviderRegistrationTests
{
    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static TOptions Resolve<TOptions>(IServiceCollection services, string scheme)
        where TOptions : class
        => services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<TOptions>>().Get(scheme);

    // ── Configuration guards ─────────────────────────────────────────────

    [Fact]
    public void A_provider_with_no_credentials_fails_at_startup()
    {
        var services = NewServices();

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddKyrolusGoogleAuth(_ => { }));

        exception.Message.ShouldContain("ClientId, ClientSecret");
    }

    [Fact]
    public void A_provider_with_no_credentials_can_be_registered_deliberately_disabled()
    {
        var services = NewServices();

        services.AddKyrolusGoogleAuth(o => o.ThrowIfNotConfigured = false);

        var descriptor = services.BuildServiceProvider()
            .GetRequiredService<IEnumerable<IKyrolusExternalAuthProvider>>()
            .Single();

        descriptor.ProviderName.ShouldBe(KyrolusAuthConstants.Providers.Google);
        descriptor.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void A_second_scheme_without_its_own_callback_path_fails_at_startup()
    {
        var services = NewServices();

        // Both schemes would keep /signin-google, and the second registration would quietly
        // shadow the first at request time.
        var exception = Should.Throw<InvalidOperationException>(() => services.AddKyrolusGoogleAuth(o =>
        {
            o.ClientId = "id";
            o.ClientSecret = "secret";
            o.SchemeName = "Google-Tenant2";
        }));

        exception.Message.ShouldContain("CallbackPath");
    }

    [Fact]
    public void A_second_scheme_with_its_own_callback_path_registers()
    {
        var services = NewServices();

        services.AddKyrolusGoogleAuth(o =>
        {
            o.ClientId = "id-1";
            o.ClientSecret = "secret-1";
        });
        services.AddKyrolusGoogleAuth(o =>
        {
            o.ClientId = "id-2";
            o.ClientSecret = "secret-2";
            o.SchemeName = "Google-Tenant2";
            o.CallbackPath = "/signin-google-tenant2";
            o.DisplayName = "Contoso Google";
        });

        var providers = services.BuildServiceProvider()
            .GetRequiredService<IEnumerable<IKyrolusExternalAuthProvider>>()
            .ToList();

        providers.Count.ShouldBe(2);
        providers.Select(p => p.AuthenticationScheme).ShouldBe(["Google", "Google-Tenant2"], ignoreOrder: true);
        providers.Single(p => p.AuthenticationScheme == "Google-Tenant2").DisplayName.ShouldBe("Contoso Google");
    }

    // ── Shared options plumbing ──────────────────────────────────────────

    [Fact]
    public void Shared_options_reach_the_underlying_handler()
    {
        var services = NewServices();

        services.AddKyrolusGoogleAuth(o =>
        {
            o.ClientId = "id";
            o.ClientSecret = "secret";
            o.SaveTokens = false;
            o.CallbackPath = "/oauth/google";
            o.BackchannelTimeout = TimeSpan.FromSeconds(7);
            o.Scopes.Add("https://www.googleapis.com/auth/calendar.readonly");
            o.ClaimMappings.Add(new KyrolusClaimMapping("hd", "hosted_domain"));
        });

        var google = Resolve<GoogleOptions>(services, "Google");

        google.SaveTokens.ShouldBeFalse();
        google.CallbackPath.Value.ShouldBe("/oauth/google");
        google.BackchannelTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        google.Scope.ShouldContain("https://www.googleapis.com/auth/calendar.readonly");
        google.Scope.ShouldContain("email");
    }

    [Fact]
    public void Google_sends_hd_and_prompt_as_authorization_parameters()
    {
        var services = NewServices();

        services.AddKyrolusGoogleAuth(o =>
        {
            o.ClientId = "id";
            o.ClientSecret = "secret";
            o.HostedDomain = "contoso.com";
            o.Prompt = "consent";
            o.RequestRefreshToken = true;
        });

        var google = Resolve<GoogleOptions>(services, "Google");

        // The old approach appended "?hd=..." to AuthorizationEndpoint, which produced a
        // malformed URL and skipped encoding entirely.
        google.AuthorizationEndpoint.ShouldNotContain("hd=");
        google.AdditionalAuthorizationParameters["hd"].ShouldBe("contoso.com");
        google.AdditionalAuthorizationParameters["prompt"].ShouldBe("consent");
        google.AccessType.ShouldBe("offline");
    }

    [Fact]
    public void The_provider_escape_hatch_runs_last()
    {
        var services = NewServices();

        services.AddKyrolusGoogleAuth(
            o =>
            {
                o.ClientId = "id";
                o.ClientSecret = "secret";
                o.SaveTokens = true;
            },
            google => google.SaveTokens = false);

        Resolve<GoogleOptions>(services, "Google").SaveTokens.ShouldBeFalse();
    }

    // ── Per-provider behaviour ───────────────────────────────────────────

    [Fact]
    public void Facebook_sends_the_app_secret_proof_by_default()
    {
        var services = NewServices();

        services.AddKyrolusFacebookAuth(o =>
        {
            o.AppId = "id";
            o.AppSecret = "secret";
            o.Fields.Add("birthday");
        });

        var facebook = Resolve<FacebookOptions>(services, "Facebook");

        facebook.SendAppSecretProof.ShouldBeTrue();
        facebook.Fields.ShouldContain("birthday");
    }

    [Fact]
    public void GitHub_derives_its_enterprise_endpoints_from_the_domain()
    {
        var services = NewServices();

        services.AddKyrolusGitHubAuth(o =>
        {
            o.ClientId = "id";
            o.ClientSecret = "secret";
            o.EnterpriseDomain = "github.contoso.com";
        });

        var github = Resolve<GitHubAuthenticationOptions>(services, "GitHub");

        github.AuthorizationEndpoint.ShouldStartWith("https://github.contoso.com/");
        github.TokenEndpoint.ShouldStartWith("https://github.contoso.com/");

        // Enterprise Server serves its API at {domain}/api/v3, never at api.{domain}: the old
        // hand-rolled endpoints pointed at a host that does not exist.
        github.UserInformationEndpoint.ShouldStartWith("https://github.contoso.com/api/v3");
        github.UserInformationEndpoint.ShouldNotContain("api.github.contoso.com");
    }

    [Fact]
    public void X_requires_pkce_and_adds_offline_access_for_refresh_tokens()
    {
        var services = NewServices();

        services.AddKyrolusXAuth(o =>
        {
            o.ClientId = "id";
            o.ClientSecret = "secret";
            o.RequestRefreshToken = true;
            o.UserFields.Add("profile_image_url");
        });

        var twitter = Resolve<TwitterAuthenticationOptions>(services, "X");

        twitter.UsePkce.ShouldBeTrue();
        twitter.Scope.ShouldContain("offline.access");
        twitter.UserFields.ShouldContain("profile_image_url");
    }

    [Fact]
    public void Microsoft_points_at_the_tenant_specific_authority()
    {
        var services = NewServices();

        services.AddKyrolusMicrosoftAuth(o =>
        {
            o.ClientId = "id";
            o.ClientSecret = "secret";
            o.Tenant = "contoso.onmicrosoft.com";
            o.DomainHint = "contoso.com";
        });

        var microsoft = Resolve<MicrosoftAccountOptions>(services, "Microsoft");

        microsoft.AuthorizationEndpoint.ShouldBe(
            "https://login.microsoftonline.com/contoso.onmicrosoft.com/oauth2/v2.0/authorize");
        microsoft.TokenEndpoint.ShouldBe(
            "https://login.microsoftonline.com/contoso.onmicrosoft.com/oauth2/v2.0/token");
        microsoft.AdditionalAuthorizationParameters["domain_hint"].ShouldBe("contoso.com");
    }

    [Fact]
    public void Microsoft_keeps_the_common_authority_by_default()
    {
        var services = NewServices();

        services.AddKyrolusMicrosoftAuth(o =>
        {
            o.ClientId = "id";
            o.ClientSecret = "secret";
        });

        Resolve<MicrosoftAccountOptions>(services, "Microsoft")
            .AuthorizationEndpoint.ShouldContain("/common/");
    }

    [Fact]
    public void Apple_refuses_two_private_key_sources()
    {
        var services = NewServices();

        Should.Throw<InvalidOperationException>(() => services.AddKyrolusAppleAuth(o =>
        {
            o.ClientId = "com.contoso.web";
            o.TeamId = "ABCDE12345";
            o.KeyId = "K1234ABCD5";
            o.PrivateKeyPath = "/certs/key.p8";
            o.PrivateKeyPem = "-----BEGIN PRIVATE KEY-----";
        })).Message.ShouldContain("not both");
    }

    [Fact]
    public void Apple_refuses_a_client_secret_lifetime_longer_than_six_months()
    {
        var services = NewServices();

        Should.Throw<InvalidOperationException>(() => services.AddKyrolusAppleAuth(o =>
        {
            o.ClientId = "com.contoso.web";
            o.TeamId = "ABCDE12345";
            o.KeyId = "K1234ABCD5";
            o.PrivateKeyPem = "-----BEGIN PRIVATE KEY-----";
            o.ClientSecretExpiresAfter = TimeSpan.FromDays(365);
        })).Message.ShouldContain("6 months");
    }

    [Fact]
    public void Apple_is_not_considered_configured_without_a_signing_key()
    {
        var services = NewServices();

        // Apple has no static client secret; without the .p8 key the handler cannot mint one.
        Should.Throw<InvalidOperationException>(() => services.AddKyrolusAppleAuth(o =>
        {
            o.ClientId = "com.contoso.web";
            o.TeamId = "ABCDE12345";
            o.KeyId = "K1234ABCD5";
        })).Message.ShouldContain("PrivateKeyPath");
    }

    [Fact]
    public void Every_provider_registers_a_descriptor()
    {
        var services = NewServices();

        services.AddKyrolusGoogleAuth(o => { o.ClientId = "a"; o.ClientSecret = "b"; });
        services.AddKyrolusFacebookAuth(o => { o.AppId = "a"; o.AppSecret = "b"; });
        services.AddKyrolusGitHubAuth(o => { o.ClientId = "a"; o.ClientSecret = "b"; });
        services.AddKyrolusXAuth(o => { o.ClientId = "a"; o.ClientSecret = "b"; });
        services.AddKyrolusMicrosoftAuth(o => { o.ClientId = "a"; o.ClientSecret = "b"; });
        services.AddKyrolusLinkedInAuth(o => { o.ClientId = "a"; o.ClientSecret = "b"; });
        services.AddKyrolusDiscordAuth(o => { o.ClientId = "a"; o.ClientSecret = "b"; });
        services.AddKyrolusAppleAuth(o =>
        {
            o.ClientId = "com.contoso.web";
            o.TeamId = "ABCDE12345";
            o.KeyId = "K1234ABCD5";
            o.PrivateKeyPem = "-----BEGIN PRIVATE KEY-----";
        });

        var providers = services.BuildServiceProvider()
            .GetRequiredService<IEnumerable<IKyrolusExternalAuthProvider>>()
            .ToList();

        providers.Count.ShouldBe(8);
        providers.ShouldAllBe(p => p.IsConfigured);
        providers.Select(p => p.ProviderName).ShouldBe(
            [
                KyrolusAuthConstants.Providers.Google,
                KyrolusAuthConstants.Providers.Facebook,
                KyrolusAuthConstants.Providers.GitHub,
                KyrolusAuthConstants.Providers.X,
                KyrolusAuthConstants.Providers.Microsoft,
                KyrolusAuthConstants.Providers.LinkedIn,
                KyrolusAuthConstants.Providers.Discord,
                KyrolusAuthConstants.Providers.Apple,
            ],
            ignoreOrder: true);
    }
}
