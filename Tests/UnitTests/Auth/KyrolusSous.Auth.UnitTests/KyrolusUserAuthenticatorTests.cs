using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Runtime;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

public sealed class KyrolusUserAuthenticatorTests
{
    private readonly KyrolusInMemoryAuthUserStore _store = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly KyrolusPbkdf2PasswordHasher _hasher =
        new(Options.Create(new KyrolusAuthOptions { Pbkdf2Iterations = 10_000 }));

    private KyrolusUserAuthenticator CreateAuthenticator(
        Action<KyrolusAuthOptions>? configure = null,
        bool withLockoutStore = true)
    {
        var options = new KyrolusAuthOptions { Pbkdf2Iterations = 10_000 };
        configure?.Invoke(options);

        return new KyrolusUserAuthenticator(
            _store,
            _hasher,
            Options.Create(options),
            _time,
            withLockoutStore ? _store : null);
    }

    private KyrolusAuthUser SeedUser(string userName = "ada", string password = "s3cret!", Action<KyrolusAuthUser>? configure = null)
    {
        var user = new KyrolusAuthUser
        {
            UserName = userName,
            Email = $"{userName}@contoso.com",
            EmailConfirmed = true,
            PasswordHash = _hasher.Hash(password),
        };

        configure?.Invoke(user);
        return _store.Add(user);
    }

    [Fact(DisplayName = "Authenticate succeeds with the right password")]
    public async Task Authenticate_succeeds_with_the_right_password()
    {
        var user = SeedUser();

        var result = await CreateAuthenticator().AuthenticateAsync("ada", "s3cret!");

        result.Succeeded.ShouldBeTrue();
        result.User!.Id.ShouldBe(user.Id);
    }

    [Fact(DisplayName = "Authenticate succeeds with the email address")]
    public async Task Authenticate_succeeds_with_the_email_address()
    {
        SeedUser();

        var result = await CreateAuthenticator().AuthenticateAsync("ada@contoso.com", "s3cret!");

        result.Succeeded.ShouldBeTrue();
    }

    [Fact(DisplayName = "Authenticate refuses the email address when email sign in is off")]
    public async Task Authenticate_refuses_the_email_address_when_email_sign_in_is_off()
    {
        SeedUser();

        var result = await CreateAuthenticator(o => o.AllowSignInWithEmail = false)
            .AuthenticateAsync("ada@contoso.com", "s3cret!");

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.InvalidCredentials);
    }

    [Fact(DisplayName = "Authenticate reports the same error for a wrong password and an unknown user")]
    public async Task Authenticate_reports_the_same_error_for_a_wrong_password_and_an_unknown_user()
    {
        SeedUser();
        var authenticator = CreateAuthenticator();

        var wrongPassword = await authenticator.AuthenticateAsync("ada", "nope");
        var unknownUser = await authenticator.AuthenticateAsync("grace", "s3cret!");

        // Distinguishing the two would turn the endpoint into a user-enumeration oracle.
        wrongPassword.ErrorCode.ShouldBe(unknownUser.ErrorCode);
        wrongPassword.ErrorDescription.ShouldBe(unknownUser.ErrorDescription);
        wrongPassword.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.InvalidCredentials);
    }

    [Fact(DisplayName = "Authenticate refuses a disabled account")]
    public async Task Authenticate_refuses_a_disabled_account()
    {
        SeedUser(configure: u => u.IsActive = false);

        var result = await CreateAuthenticator().AuthenticateAsync("ada", "s3cret!");

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.UserInactive);
    }

    [Fact(DisplayName = "Authenticate refuses an unconfirmed email when configured to")]
    public async Task Authenticate_refuses_an_unconfirmed_email_when_configured_to()
    {
        SeedUser(configure: u => u.EmailConfirmed = false);

        var result = await CreateAuthenticator(o => o.RequireConfirmedEmail = true)
            .AuthenticateAsync("ada", "s3cret!");

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.EmailNotConfirmed);
    }

    [Fact(DisplayName = "Authenticate refuses a user with no password hash")]
    public async Task Authenticate_refuses_a_user_with_no_password_hash()
    {
        // An account that only ever signs in through an external provider.
        SeedUser(configure: u => u.PasswordHash = null);

        var result = await CreateAuthenticator().AuthenticateAsync("ada", "anything");

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.InvalidCredentials);
    }

    [Fact(DisplayName = "Repeated failures lock the account out")]
    public async Task Repeated_failures_lock_the_account_out()
    {
        var user = SeedUser();
        var authenticator = CreateAuthenticator(o =>
        {
            o.MaxFailedAccessAttempts = 3;
            o.LockoutDuration = TimeSpan.FromMinutes(5);
        });

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await authenticator.AuthenticateAsync("ada", "nope");
        }

        user.AccessFailedCount.ShouldBe(3);
        user.LockoutEnd.ShouldBe(_time.GetUtcNow().AddMinutes(5));

        // Even the right password is refused while the lockout stands.
        var locked = await authenticator.AuthenticateAsync("ada", "s3cret!");
        locked.Succeeded.ShouldBeFalse();
        locked.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.UserLockedOut);
    }

    [Fact(DisplayName = "A lockout expires")]
    public async Task A_lockout_expires()
    {
        SeedUser();
        var authenticator = CreateAuthenticator(o =>
        {
            o.MaxFailedAccessAttempts = 1;
            o.LockoutDuration = TimeSpan.FromMinutes(5);
        });

        await authenticator.AuthenticateAsync("ada", "nope");
        _time.Advance(TimeSpan.FromMinutes(6));

        var result = await authenticator.AuthenticateAsync("ada", "s3cret!");

        result.Succeeded.ShouldBeTrue();
    }

    [Fact(DisplayName = "A successful sign in clears the failure counter")]
    public async Task A_successful_sign_in_clears_the_failure_counter()
    {
        var user = SeedUser();
        var authenticator = CreateAuthenticator(o => o.MaxFailedAccessAttempts = 5);

        await authenticator.AuthenticateAsync("ada", "nope");
        user.AccessFailedCount.ShouldBe(1);

        await authenticator.AuthenticateAsync("ada", "s3cret!");

        user.AccessFailedCount.ShouldBe(0);
        user.LockoutEnd.ShouldBeNull();
    }

    [Fact(DisplayName = "Failures are not counted without a lockout store")]
    public async Task Failures_are_not_counted_without_a_lockout_store()
    {
        var user = SeedUser();
        var authenticator = CreateAuthenticator(withLockoutStore: false);

        await authenticator.AuthenticateAsync("ada", "nope");

        user.AccessFailedCount.ShouldBe(0);
    }

    [Theory(DisplayName = "Authenticate rejects empty input")]
    [InlineData("", "pw")]
    [InlineData("   ", "pw")]
    [InlineData("ada", "")]
    public async Task Authenticate_rejects_empty_input(string userName, string password)
    {
        SeedUser();

        var result = await CreateAuthenticator().AuthenticateAsync(userName, password);

        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.InvalidCredentials);
    }

    [Fact(DisplayName = "Authenticate succeeds with padded whitespace in username or email")]
    public async Task Authenticate_succeeds_with_padded_whitespace_in_username_or_email()
    {
        var user = SeedUser("grace", "p@ssword!");

        // Whitespace around username
        var resultUser = await CreateAuthenticator().AuthenticateAsync("  grace  ", "p@ssword!");
        resultUser.Succeeded.ShouldBeTrue();
        resultUser.User!.Id.ShouldBe(user.Id);

        // Whitespace around email
        var resultEmail = await CreateAuthenticator().AuthenticateAsync("  grace@contoso.com \t ", "p@ssword!");
        resultEmail.Succeeded.ShouldBeTrue();
        resultEmail.User!.Id.ShouldBe(user.Id);
    }

    [Fact(DisplayName = "Authenticate Rejects Oversized Inputs")]
    public async Task Authenticate_Rejects_Oversized_Inputs()
    {
        SeedUser("grace", "p@ssword!");

        var giantPassword = new string('A', 5000);
        var giantIdentifier = new string('B', 300);

        var res1 = await CreateAuthenticator().AuthenticateAsync("grace", giantPassword);
        res1.Succeeded.ShouldBeFalse();
        res1.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.InvalidCredentials);

        var res2 = await CreateAuthenticator().AuthenticateAsync(giantIdentifier, "p@ssword!");
        res2.Succeeded.ShouldBeFalse();
        res2.ErrorCode.ShouldBe(KyrolusAuthConstants.Errors.InvalidCredentials);
    }

    [Fact(DisplayName = "Kyrolus Auth User Add Role Deduplicates And Trims")]
    public void KyrolusAuthUser_AddRole_DeduplicatesAndTrims()
    {
        var user = new KyrolusAuthUser();
        user.AddRole("Admin");
        user.AddRole("  ADMIN  ");
        user.AddRole("  ");
        user.AddRole("Editor");

        user.Roles.Count.ShouldBe(2);
        user.Roles.ShouldContain("Admin");
        user.Roles.ShouldContain("Editor");
    }

    [Fact(DisplayName = "Password Hasher Verify Fast Fails Oversized Password")]
    public void PasswordHasher_Verify_FastFails_OversizedPassword()
    {
        var hasher = new KyrolusPbkdf2PasswordHasher(Microsoft.Extensions.Options.Options.Create(new KyrolusAuthOptions()));
        var hash = hasher.Hash("validPassword123!");
        var giantPassword = new string('A', 5000);

        var result = hasher.Verify(hash, giantPassword);
        result.ShouldBe(KyrolusPasswordVerificationResult.Failed);
    }

    [Fact(DisplayName = "Claims Principal Factory Throws When User Id Is Null Or Whitespace")]
    public async Task ClaimsPrincipalFactory_Throws_WhenUserIdIsNullOrWhitespace()
    {
        var factory = new KyrolusClaimsPrincipalFactory(Microsoft.Extensions.Options.Options.Create(new KyrolusAuthOptions()));
        var user = new KyrolusAuthUser { Id = "  ", UserName = "invalid" };

        await Should.ThrowAsync<ArgumentException>(async () =>
            await factory.CreateAsync(user, ["profile"], "test"));
    }

    [Fact(DisplayName = "In Memory Auth User Store Find By Id Async Returns Null For Whitespace")]
    public async Task InMemoryAuthUserStore_FindByIdAsync_ReturnsNull_ForWhitespace()
    {
        var store = new KyrolusInMemoryAuthUserStore();
        var user = await store.FindByIdAsync("   ");
        user.ShouldBeNull();
    }
}
