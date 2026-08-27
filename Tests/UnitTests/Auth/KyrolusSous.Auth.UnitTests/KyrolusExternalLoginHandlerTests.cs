using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Google;
using KyrolusSous.Auth.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

public sealed class KyrolusExternalLoginHandlerTests
{
    private readonly KyrolusInMemoryAuthUserStore _store = new();

    private KyrolusExternalLoginHandler CreateHandler()
        => new(_store, NullLogger<KyrolusExternalLoginHandler>.Instance);

    private static KyrolusExternalLoginInfo LoginInfo(
        string providerKey = "google-123",
        string? email = "ada@contoso.com",
        bool emailVerified = true)
        => new()
        {
            ProviderName = KyrolusAuthConstants.Providers.Google,
            ProviderKey = providerKey,
            Principal = new ClaimsPrincipal(new ClaimsIdentity()),
            Email = email,
            EmailVerified = emailVerified,
            DisplayName = "Ada Lovelace",
        };

    [Fact]
    public async Task An_already_linked_identity_signs_in()
    {
        var user = _store.Add(new KyrolusAuthUser { UserName = "ada", Roles = { "Admin" } });
        await _store.AddExternalLoginAsync(user.Id, KyrolusAuthConstants.Providers.Google, "google-123");

        var result = await CreateHandler().HandleAsync(LoginInfo(), new KyrolusGoogleAuthOptions());

        result.Succeeded.ShouldBeTrue();
        result.AdditionalClaims.ShouldContain(c =>
            c.Type == KyrolusAuthConstants.Claims.Sub && c.Value == user.Id);
        result.AdditionalClaims.ShouldContain(c =>
            c.Type == KyrolusAuthConstants.Claims.Role && c.Value == "Admin");
    }

    [Fact]
    public async Task An_unknown_identity_is_refused_when_provisioning_is_off()
    {
        var result = await CreateHandler().HandleAsync(LoginInfo(), new KyrolusGoogleAuthOptions());

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.UserNotFound);
    }

    [Fact]
    public async Task An_identity_with_no_subject_is_refused()
    {
        var options = new KyrolusGoogleAuthOptions { AutoCreateUser = true };

        var result = await CreateHandler().HandleAsync(LoginInfo(providerKey: ""), options);

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.ExternalLoginFailed);
    }

    [Fact]
    public async Task A_verified_email_links_to_an_existing_account_when_allowed()
    {
        var existing = _store.Add(new KyrolusAuthUser { UserName = "ada", Email = "ada@contoso.com" });
        var options = new KyrolusGoogleAuthOptions { LinkToExistingUserByEmail = true };

        var result = await CreateHandler().HandleAsync(LoginInfo(), options);

        result.Succeeded.ShouldBeTrue();
        result.AdditionalClaims.ShouldContain(c =>
            c.Type == KyrolusAuthConstants.Claims.Sub && c.Value == existing.Id);

        var linked = await _store.FindByExternalLoginAsync(KyrolusAuthConstants.Providers.Google, "google-123");
        linked!.Id.ShouldBe(existing.Id);
    }

    [Fact]
    public async Task An_unverified_email_never_links_to_an_existing_account()
    {
        _store.Add(new KyrolusAuthUser { UserName = "ada", Email = "ada@contoso.com" });
        var options = new KyrolusGoogleAuthOptions { LinkToExistingUserByEmail = true };

        // Linking on an unverified address is account takeover: anyone who can register that
        // address at a lax provider inherits the local account.
        var result = await CreateHandler().HandleAsync(LoginInfo(emailVerified: false), options);

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.UserNotFound);
    }

    [Fact]
    public async Task Linking_by_email_stays_off_unless_it_is_asked_for()
    {
        _store.Add(new KyrolusAuthUser { UserName = "ada", Email = "ada@contoso.com" });

        var result = await CreateHandler().HandleAsync(LoginInfo(), new KyrolusGoogleAuthOptions());

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task An_unknown_identity_is_provisioned_when_asked_for()
    {
        var options = new KyrolusGoogleAuthOptions { AutoCreateUser = true, DefaultRole = "Member" };

        var result = await CreateHandler().HandleAsync(LoginInfo(), options);

        result.Succeeded.ShouldBeTrue();

        var created = await _store.FindByExternalLoginAsync(KyrolusAuthConstants.Providers.Google, "google-123");
        created.ShouldNotBeNull();
        created.Email.ShouldBe("ada@contoso.com");
        created.EmailConfirmed.ShouldBeTrue();
        created.DisplayName.ShouldBe("Ada Lovelace");
        created.Roles.ShouldContain("Member");
    }

    [Fact]
    public async Task A_provisioned_user_without_an_email_gets_a_provider_scoped_user_name()
    {
        var options = new KyrolusGoogleAuthOptions { AutoCreateUser = true };

        await CreateHandler().HandleAsync(LoginInfo(email: null), options);

        var created = await _store.FindByExternalLoginAsync(KyrolusAuthConstants.Providers.Google, "google-123");
        created!.UserName.ShouldBe("google:google-123");
    }

    [Fact]
    public async Task A_disabled_account_is_refused()
    {
        var user = _store.Add(new KyrolusAuthUser { UserName = "ada", IsActive = false });
        await _store.AddExternalLoginAsync(user.Id, KyrolusAuthConstants.Providers.Google, "google-123");

        var result = await CreateHandler().HandleAsync(LoginInfo(), new KyrolusGoogleAuthOptions());

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.UserInactive);
    }

    [Fact]
    public async Task A_provisioned_user_inherits_DefaultTenantId_and_trimmed_email()
    {
        var options = new KyrolusGoogleAuthOptions
        {
            AutoCreateUser = true,
            DefaultTenantId = "tenant-enterprise-1"
        };

        var info = LoginInfo(providerKey: "g-tenant-user", email: "  tenant.user@company.com  ");
        var result = await CreateHandler().HandleAsync(info, options);

        result.Succeeded.ShouldBeTrue();
        result.AdditionalClaims.ShouldContain(c =>
            c.Type == KyrolusAuthConstants.Claims.TenantId && c.Value == "tenant-enterprise-1");

        var user = await _store.FindByExternalLoginAsync(KyrolusAuthConstants.Providers.Google, "g-tenant-user");
        user.ShouldNotBeNull();
        user.TenantId.ShouldBe("tenant-enterprise-1");
        user.Email.ShouldBe("tenant.user@company.com");
    }
}
