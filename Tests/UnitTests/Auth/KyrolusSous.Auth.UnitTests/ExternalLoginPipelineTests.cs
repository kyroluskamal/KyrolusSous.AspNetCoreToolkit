using System.Security.Claims;
using System.Text.Json;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

/// <summary>
/// Exercises the ticket pipeline <see cref="KyrolusExternalAuthConfigurator"/> installs, without
/// standing up a real OAuth round trip.
/// </summary>
public sealed class ExternalLoginPipelineTests
{
    private static (GoogleOptions Provider, OAuthCreatingTicketContext Context) Build(
        KyrolusGoogleAuthOptions kyrolusOptions,
        IKyrolusExternalLoginHandler? handler = null,
        Action<ClaimsIdentity>? claims = null)
    {
        var services = new ServiceCollection();
        if (handler is not null)
        {
            services.AddSingleton(handler);
        }

        var provider = new GoogleOptions { ClientId = "id", ClientSecret = "secret" };
        KyrolusExternalAuthConfigurator.Apply(provider, kyrolusOptions, KyrolusAuthConstants.Providers.Google);

        var identity = new ClaimsIdentity(KyrolusAuthConstants.Providers.Google);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "google-123"));
        claims?.Invoke(identity);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        using var payload = JsonDocument.Parse("{}");
        var context = new OAuthCreatingTicketContext(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties(),
            httpContext,
            new AuthenticationScheme(
                KyrolusAuthConstants.Providers.Google,
                KyrolusAuthConstants.Providers.Google,
                typeof(GoogleHandler)),
            provider,
            new HttpClient(),
            OAuthTokenResponse.Success(JsonDocument.Parse("""{"access_token":"at","refresh_token":"rt"}""")),
            payload.RootElement.Clone());

        return (provider, context);
    }

    [Fact(DisplayName = "A refused login aborts the sign in")]
    public async Task A_refused_login_aborts_the_sign_in()
    {
        var handler = new StubHandler(KyrolusExternalLoginResult.Fail(
            KyrolusAuthConstants.Errors.UserNotFound, "No local account."));

        var (provider, context) = Build(new KyrolusGoogleAuthOptions(), handler);

        // Refusal has to throw. RemoteAuthenticationHandler ignores a failed AuthenticateResult
        // set from OnTicketReceived and signs the user in regardless, so returning a failure
        // there would let a rejected identity through.
        var exception = await Should.ThrowAsync<KyrolusExternalLoginException>(
            () => provider.Events.OnCreatingTicket(context));

        exception.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.UserNotFound);
        exception.ProviderName.ShouldBe(KyrolusAuthConstants.Providers.Google);
        exception.Message.ShouldBe("No local account.");
    }

    [Fact(DisplayName = "An unverified email is refused when verification is required")]
    public async Task An_unverified_email_is_refused_when_verification_is_required()
    {
        var options = new KyrolusGoogleAuthOptions { RequireVerifiedEmail = true };
        var (provider, context) = Build(options, claims: identity =>
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, "ada@contoso.com"));
            identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.EmailVerified, "false"));
        });

        var exception = await Should.ThrowAsync<KyrolusExternalLoginException>(
            () => provider.Events.OnCreatingTicket(context));

        exception.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.ExternalLoginDenied);
    }

    [Fact(DisplayName = "A verified email passes the verification requirement")]
    public async Task A_verified_email_passes_the_verification_requirement()
    {
        var options = new KyrolusGoogleAuthOptions { RequireVerifiedEmail = true };
        var (provider, context) = Build(options, claims: identity =>
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, "ada@contoso.com"));
            identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.EmailVerified, "true"));
        });

        await provider.Events.OnCreatingTicket(context);

        context.Principal!.FindFirst(KyrolusAuthConstants.Claims.Provider)!.Value
            .ShouldBe(KyrolusAuthConstants.Providers.Google);
    }

    [Fact(DisplayName = "The provider and subject are stamped onto the principal")]
    public async Task The_provider_and_subject_are_stamped_onto_the_principal()
    {
        var (provider, context) = Build(new KyrolusGoogleAuthOptions());

        await provider.Events.OnCreatingTicket(context);

        context.Principal!.FindFirst(KyrolusAuthConstants.Claims.Provider)!.Value
            .ShouldBe(KyrolusAuthConstants.Providers.Google);
        context.Principal.FindFirst(KyrolusAuthConstants.Claims.ProviderKey)!.Value
            .ShouldBe("google-123");
    }

    [Fact(DisplayName = "Local claims from the handler are merged into the principal")]
    public async Task Local_claims_from_the_handler_are_merged_into_the_principal()
    {
        var handler = new StubHandler(KyrolusExternalLoginResult.Success(
        [
            new Claim(KyrolusAuthConstants.Claims.Sub, "user-1"),
            new Claim(KyrolusAuthConstants.Claims.Role, "Admin"),
        ]));

        var (provider, context) = Build(new KyrolusGoogleAuthOptions(), handler);

        await provider.Events.OnCreatingTicket(context);

        context.Principal!.FindFirst(KyrolusAuthConstants.Claims.Sub)!.Value.ShouldBe("user-1");
        context.Principal.FindFirst(KyrolusAuthConstants.Claims.Role)!.Value.ShouldBe("Admin");

        // The subject from the provider is left alone: it is the link back to the external identity.
        context.Principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.ShouldBe("google-123");
    }

    [Fact(DisplayName = "A handler can replace the principal outright")]
    public async Task A_handler_can_replace_the_principal_outright()
    {
        var replacement = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(KyrolusAuthConstants.Claims.Sub, "user-9")], "replaced"));

        var (provider, context) = Build(
            new KyrolusGoogleAuthOptions(),
            new StubHandler(KyrolusExternalLoginResult.Success(replacement)));

        await provider.Events.OnCreatingTicket(context);

        context.Principal!.Identity!.AuthenticationType.ShouldBe("replaced");
        context.Principal.FindFirst(KyrolusAuthConstants.Claims.Sub)!.Value.ShouldBe("user-9");
    }

    [Fact(DisplayName = "The handler sees the normalised identity")]
    public async Task The_handler_sees_the_normalised_identity()
    {
        var handler = new StubHandler(KyrolusExternalLoginResult.Success());
        var options = new KyrolusGoogleAuthOptions();

        var (provider, context) = Build(options, handler, identity =>
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, "ada@contoso.com"));
            identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.EmailVerified, "True"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "Ada Lovelace"));
            identity.AddClaim(new Claim(ClaimTypes.GivenName, "Ada"));
            identity.AddClaim(new Claim(ClaimTypes.Surname, "Lovelace"));
            identity.AddClaim(new Claim("urn:google:picture", "https://example.test/ada.png"));
        });

        await provider.Events.OnCreatingTicket(context);

        var info = handler.Received.ShouldNotBeNull();
        info.ProviderName.ShouldBe(KyrolusAuthConstants.Providers.Google);
        info.ProviderKey.ShouldBe("google-123");
        info.Email.ShouldBe("ada@contoso.com");
        info.EmailVerified.ShouldBeTrue();   // GitHub sends "True", Google a JSON boolean
        info.DisplayName.ShouldBe("Ada Lovelace");
        info.GivenName.ShouldBe("Ada");
        info.FamilyName.ShouldBe("Lovelace");
        info.PictureUrl.ShouldBe("https://example.test/ada.png");
        info.Tokens[KyrolusAuthConstants.Tokens.AccessToken].ShouldBe("at");
        info.Tokens[KyrolusAuthConstants.Tokens.RefreshToken].ShouldBe("rt");
        handler.ReceivedOptions.ShouldBeSameAs(options);
    }

    [Fact(DisplayName = "A caller supplied creating ticket handler still runs")]
    public async Task A_caller_supplied_creating_ticket_handler_still_runs()
    {
        var options = new KyrolusGoogleAuthOptions();
        var provider = new GoogleOptions { ClientId = "id", ClientSecret = "secret" };

        var ran = false;
        provider.Events.OnCreatingTicket = _ =>
        {
            ran = true;
            return Task.CompletedTask;
        };

        KyrolusExternalAuthConfigurator.Apply(provider, options, KyrolusAuthConstants.Providers.Google);

        var (_, context) = Build(options);
        await provider.Events.OnCreatingTicket(context);

        ran.ShouldBeTrue();
    }

    [Fact(DisplayName = "An external login without a valid provider key is rejected")]
    public async Task An_external_login_without_a_valid_provider_key_is_rejected()
    {
        var options = new KyrolusGoogleAuthOptions();
        var (provider, context) = Build(options, claims: identity =>
        {
            var sub = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (sub is not null) identity.RemoveClaim(sub);
        });

        var ex = await Should.ThrowAsync<KyrolusExternalLoginException>(() =>
            provider.Events.OnCreatingTicket(context));

        ex.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.ExternalLoginFailed);
        ex.Message.ShouldContain("did not report a valid user identifier");
    }

    private sealed class StubHandler(KyrolusExternalLoginResult result) : IKyrolusExternalLoginHandler
    {
        public KyrolusExternalLoginInfo? Received { get; private set; }

        public KyrolusExternalLoginOptions? ReceivedOptions { get; private set; }

        public ValueTask<KyrolusExternalLoginResult> HandleAsync(
            KyrolusExternalLoginInfo info,
            KyrolusExternalLoginOptions options,
            CancellationToken cancellationToken = default)
        {
            Received = info;
            ReceivedOptions = options;
            return ValueTask.FromResult(result);
        }
    }
}
