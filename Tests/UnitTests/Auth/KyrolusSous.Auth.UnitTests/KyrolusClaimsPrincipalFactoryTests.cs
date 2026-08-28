using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Runtime;
using Microsoft.Extensions.Options;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

public sealed class KyrolusClaimsPrincipalFactoryTests
{
    private static readonly KyrolusAuthUser User = new()
    {
        Id = "user-1",
        UserName = "ada",
        DisplayName = "Ada Lovelace",
        Email = "ada@contoso.com",
        EmailConfirmed = true,
        PhoneNumber = "+201234567890",
        TenantId = "contoso",
        Roles = { "Admin", "Member" },
        Claims = { ["department"] = "engineering" },
    };

    private static KyrolusClaimsPrincipalFactory CreateFactory(Action<KyrolusAuthOptions>? configure = null)
    {
        var options = new KyrolusAuthOptions();
        configure?.Invoke(options);
        return new KyrolusClaimsPrincipalFactory(Options.Create(options));
    }

    [Fact(DisplayName = "The subject and username are always present")]
    public async Task The_subject_and_username_are_always_present()
    {
        var principal = await CreateFactory().CreateAsync(User, [], "test");

        principal.FindFirst(KyrolusAuthConstants.Claims.Sub)!.Value.ShouldBe("user-1");
        principal.FindFirst(KyrolusAuthConstants.Claims.PreferredUsername)!.Value.ShouldBe("ada");
    }

    [Fact(DisplayName = "The email claim needs the email scope")]
    public async Task The_email_claim_needs_the_email_scope()
    {
        var withoutScope = await CreateFactory().CreateAsync(User, ["openid"], "test");
        var withScope = await CreateFactory().CreateAsync(User, ["openid", "email"], "test");

        withoutScope.FindFirst(KyrolusAuthConstants.Claims.Email).ShouldBeNull();
        withScope.FindFirst(KyrolusAuthConstants.Claims.Email)!.Value.ShouldBe("ada@contoso.com");
        withScope.FindFirst(KyrolusAuthConstants.Claims.EmailVerified)!.Value.ShouldBe("true");
    }

    [Fact(DisplayName = "The profile claims need the profile scope")]
    public async Task The_profile_claims_need_the_profile_scope()
    {
        var withoutScope = await CreateFactory().CreateAsync(User, ["openid"], "test");
        var withScope = await CreateFactory().CreateAsync(User, ["openid", "profile"], "test");

        withoutScope.FindFirst(KyrolusAuthConstants.Claims.Name).ShouldBeNull();
        withScope.FindFirst(KyrolusAuthConstants.Claims.Name)!.Value.ShouldBe("Ada Lovelace");
    }

    [Fact(DisplayName = "The phone claims need the phone scope")]
    public async Task The_phone_claims_need_the_phone_scope()
    {
        var withScope = await CreateFactory().CreateAsync(User, ["phone"], "test");

        withScope.FindFirst(KyrolusAuthConstants.Claims.PhoneNumber)!.Value.ShouldBe("+201234567890");
        withScope.FindFirst(KyrolusAuthConstants.Claims.PhoneNumberVerified)!.Value.ShouldBe("false");
    }

    [Fact(DisplayName = "Turning scope gating off emits everything")]
    public async Task Turning_scope_gating_off_emits_everything()
    {
        var principal = await CreateFactory(o => o.EnforceScopeBasedClaims = false)
            .CreateAsync(User, ["openid"], "test");

        principal.FindFirst(KyrolusAuthConstants.Claims.Email).ShouldNotBeNull();
        principal.FindFirst(KyrolusAuthConstants.Claims.Name).ShouldNotBeNull();
        principal.FindFirst(KyrolusAuthConstants.Claims.PhoneNumber).ShouldNotBeNull();
    }

    [Fact(DisplayName = "Roles are emitted under the roles scope")]
    public async Task Roles_are_emitted_under_the_roles_scope()
    {
        var principal = await CreateFactory().CreateAsync(User, ["openid", "roles"], "test");

        principal.FindAll(KyrolusAuthConstants.Claims.Role)
            .Select(c => c.Value)
            .ShouldBe(["Admin", "Member"], ignoreOrder: true);
    }

    [Fact(DisplayName = "Roles are withheld when the roles scope was not granted")]
    public async Task Roles_are_withheld_when_the_roles_scope_was_not_granted()
    {
        var principal = await CreateFactory().CreateAsync(User, ["openid", "profile"], "test");

        principal.FindAll(KyrolusAuthConstants.Claims.Role).ShouldBeEmpty();
    }

    [Fact(DisplayName = "An empty scope set still yields roles")]
    public async Task An_empty_scope_set_still_yields_roles()
    {
        // The client credentials and legacy password grants routinely arrive with no scopes at
        // all; withholding roles there would break authorization outright.
        var principal = await CreateFactory().CreateAsync(User, [], "test");

        principal.FindAll(KyrolusAuthConstants.Claims.Role).ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "The tenant and custom claims are always emitted")]
    public async Task The_tenant_and_custom_claims_are_always_emitted()
    {
        var principal = await CreateFactory().CreateAsync(User, ["openid"], "test");

        principal.FindFirst(KyrolusAuthConstants.Claims.TenantId)!.Value.ShouldBe("contoso");
        principal.FindFirst("department")!.Value.ShouldBe("engineering");
    }

    [Fact(DisplayName = "The identity uses the requested authentication type")]
    public async Task The_identity_uses_the_requested_authentication_type()
    {
        var principal = await CreateFactory().CreateAsync(User, [], "my-scheme");

        principal.Identity!.AuthenticationType.ShouldBe("my-scheme");
        principal.Identity.IsAuthenticated.ShouldBeTrue();
    }

    [Fact(DisplayName = "Scope matching is case insensitive and handles padded scopes")]
    public async Task Scope_matching_is_case_insensitive_and_handles_padded_scopes()
    {
        var principal = await CreateFactory().CreateAsync(User, ["  PROFILE  ", "EMAIL"], "test");

        principal.FindFirst(KyrolusAuthConstants.Claims.Name)!.Value.ShouldBe("Ada Lovelace");
        principal.FindFirst(KyrolusAuthConstants.Claims.Email)!.Value.ShouldBe("ada@contoso.com");
    }

    [Fact(DisplayName = "Sensitive claims in user claims are never emitted")]
    public async Task Sensitive_claims_in_user_claims_are_never_emitted()
    {
        var userWithSensitiveClaims = new KyrolusAuthUser
        {
            Id = "user-sens",
            UserName = "sensitive",
            Claims =
            {
                ["password_hash"] = "secret_hash_value",
                ["security_stamp"] = "secret_stamp_value",
                ["concurrency_stamp"] = "secret_concurrency_value",
                ["safe_claim"] = "safe_value"
            }
        };

        var principal = await CreateFactory().CreateAsync(userWithSensitiveClaims, ["openid"], "test");

        principal.FindFirst("password_hash").ShouldBeNull();
        principal.FindFirst("security_stamp").ShouldBeNull();
        principal.FindFirst("concurrency_stamp").ShouldBeNull();
        principal.FindFirst("safe_claim")!.Value.ShouldBe("safe_value");
    }
}
