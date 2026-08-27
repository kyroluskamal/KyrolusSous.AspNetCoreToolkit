using KyrolusSous.Auth.Security;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Security.UnitTests;

public class SecurityPolicyTests
{
    private readonly KyrolusPasswordPolicyChecker _checker = new();

    [Fact]
    public void PasswordPolicy_AcceptsStrongPassword()
    {
        var result = _checker.Check("P@ssw0rdSecure!2026");

        result.Succeeded.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Score.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void PasswordPolicy_RejectsShortPassword()
    {
        var result = _checker.Check("Ab1!");

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("at least 8 characters"));
    }

    [Fact]
    public void PasswordPolicy_RejectsMissingSpecialCharacter()
    {
        var result = _checker.Check("Password12345");

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("non-alphanumeric"));
    }

    [Fact]
    public void PasswordPolicy_RejectsCommonPasswords()
    {
        var result = _checker.Check("password");

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("too common"));
    }

    [Fact]
    public async Task BruteForceGuard_LocksOutAfterMaxAttempts()
    {
        var guard = new KyrolusInMemoryBruteForceGuard(new KyrolusBruteForceOptions
        {
            MaxFailedAttempts = 3,
            LockoutDuration = TimeSpan.FromMinutes(10)
        });

        var key = "ip_192.168.1.100";

        (await guard.IsLockedOutAsync(key)).ShouldBeFalse();

        await guard.RecordFailedAttemptAsync(key);
        (await guard.IsLockedOutAsync(key)).ShouldBeFalse();

        await guard.RecordFailedAttemptAsync(key);
        (await guard.IsLockedOutAsync(key)).ShouldBeFalse();

        // 3rd failed attempt -> locked out!
        await guard.RecordFailedAttemptAsync(key);
        (await guard.IsLockedOutAsync(key)).ShouldBeTrue();

        // Reset
        await guard.ResetAsync(key);
        (await guard.IsLockedOutAsync(key)).ShouldBeFalse();
    }

    [Fact]
    public void DiRegistration_AddKyrolusAuthSecurity_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddKyrolusAuthSecurity();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusPasswordPolicyChecker>().ShouldNotBeNull();
        provider.GetService<IKyrolusBruteForceGuard>().ShouldNotBeNull();
    }

    [Fact]
    public async Task BruteForceGuard_ResetsCounter_AfterLockoutExpires()
    {
        var guard = new KyrolusInMemoryBruteForceGuard(new KyrolusBruteForceOptions
        {
            MaxFailedAttempts = 2,
            LockoutDuration = TimeSpan.FromMilliseconds(50)
        });

        var key = "user-expire-test";

        // Lock out after 2 attempts
        await guard.RecordFailedAttemptAsync(key);
        await guard.RecordFailedAttemptAsync(key);
        (await guard.IsLockedOutAsync(key)).ShouldBeTrue();

        // Wait for lockout duration to elapse
        await Task.Delay(70);

        // Lockout expired
        (await guard.IsLockedOutAsync(key)).ShouldBeFalse();

        // Next failure should reset counter to 1, NOT immediately lock out again!
        await guard.RecordFailedAttemptAsync(key);
        (await guard.IsLockedOutAsync(key)).ShouldBeFalse();
    }

    [Fact]
    public void PasswordPolicy_RejectsForbiddenPassword_WithPaddedWhitespaceOrMixedCase()
    {
        var resultUpper = _checker.Check("  PASSWORD123  ");
        resultUpper.Succeeded.ShouldBeFalse();
        resultUpper.Errors.ShouldContain(e => e.Contains("too common"));

        var resultPadded = _checker.Check("  password  ");
        resultPadded.Succeeded.ShouldBeFalse();
        resultPadded.Errors.ShouldContain(e => e.Contains("too common"));
    }

    [Fact]
    public void PasswordPolicy_DoesNotCountWhitespace_AsNonAlphanumericSpecialCharacter()
    {
        // Password has upper, lower, digit, and space (' '), but NO actual special symbol!
        var passwordWithSpaceOnly = "Pass word12345";
        var result = _checker.Check(passwordWithSpaceOnly);

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("non-alphanumeric"));
    }

    [Fact]
    public void IsPasswordPreviouslyUsed_DetectsReusedPassword()
    {
        var oldHashes = new List<string> { "hash:oldpassword1", "hash:oldpassword2", "hash:oldpassword3" };
        Func<string, string, bool> verifier = (hash, password) => hash == $"hash:{password}";

        _checker.IsPasswordPreviouslyUsed("oldpassword2", oldHashes, verifier).ShouldBeTrue();
        _checker.IsPasswordPreviouslyUsed("brandNewPassword123!", oldHashes, verifier).ShouldBeFalse();
    }

    [Fact]
    public void PasswordPolicy_FailsFast_WhenPasswordExceedsMaxLength()
    {
        var hugePassword = new string('A', 5000);
        var result = _checker.Check(hugePassword);

        result.Succeeded.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("cannot exceed");
    }

    [Fact]
    public async Task BruteForceGuard_PurgesStaleAttempts_WhenOlderThanLockoutDuration()
    {
        var guard = new KyrolusInMemoryBruteForceGuard(new KyrolusBruteForceOptions
        {
            MaxFailedAttempts = 10,
            LockoutDuration = TimeSpan.FromMilliseconds(50)
        });

        await guard.RecordFailedAttemptAsync("stale-user");
        await Task.Delay(70);

        guard.PurgeExpiredRecords();
        (await guard.IsLockedOutAsync("stale-user")).ShouldBeFalse();
    }

    [Fact]
    public void PasswordPolicy_RejectsSingleRepeatedCharacterPassword()
    {
        var result = _checker.Check("AAAAAAAA", new KyrolusPasswordPolicyOptions
        {
            RequiredUniqueChars = 1,
            RequireDigit = false,
            RequireNonAlphanumeric = false,
            RequireLowercase = false
        });

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("single repeated character"));
    }

    [Fact]
    public void PasswordPolicy_Throws_WhenMinLengthGreaterThanMaxLength()
    {
        var options = new KyrolusPasswordPolicyOptions
        {
            MinLength = 30,
            MaxLength = 10
        };

        Should.Throw<InvalidOperationException>(() =>
            _checker.Check("validPassword123!", options));
    }

    [Fact]
    public async Task BruteForceGuard_NormalizesWhitespaceInKeys()
    {
        var guard = new KyrolusInMemoryBruteForceGuard(new KyrolusBruteForceOptions
        {
            MaxFailedAttempts = 2,
            LockoutDuration = TimeSpan.FromMinutes(5)
        });

        await guard.RecordFailedAttemptAsync("  alice  ");
        await guard.RecordFailedAttemptAsync("alice");

        (await guard.IsLockedOutAsync("alice")).ShouldBeTrue();
        (await guard.IsLockedOutAsync(" alice ")).ShouldBeTrue();
    }
}
