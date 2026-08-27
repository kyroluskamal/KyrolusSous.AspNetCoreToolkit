using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.MagicLink;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.MagicLink.UnitTests;

public class MagicLinkTests
{
    private readonly KyrolusInMemoryMagicLinkStore _store = new();
    private readonly KyrolusMagicLinkService _service;

    public MagicLinkTests()
    {
        _service = new KyrolusMagicLinkService(_store);
    }

    [Fact]
    public async Task CreateMagicLink_GeneratesValidUrl()
    {
        var user = new KyrolusAuthUser
        {
            Id = "user-magic-1",
            Email = "alice@example.com"
        };

        var result = await _service.CreateMagicLinkAsync(user, "https://myapp.com/auth/callback");

        result.ShouldNotBeNull();
        result.RawToken.ShouldNotBeNullOrWhiteSpace();
        result.MagicLinkUrl.ShouldStartWith("https://myapp.com/auth/callback?token=");
        result.ExpiresAtUtc.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ValidateAndConsume_Succeeds_OnFirstUse()
    {
        var user = new KyrolusAuthUser
        {
            Id = "user-magic-2",
            Email = "bob@example.com"
        };

        var created = await _service.CreateMagicLinkAsync(user, "https://myapp.com/auth/callback");

        var validation = await _service.ValidateAndConsumeAsync(created.RawToken);

        validation.Succeeded.ShouldBeTrue();
        validation.UserId.ShouldBe("user-magic-2");
        validation.Email.ShouldBe("bob@example.com");
    }

    [Fact]
    public async Task ValidateAndConsume_Fails_OnSecondUse_PreventingReplay()
    {
        var user = new KyrolusAuthUser
        {
            Id = "user-magic-3",
            Email = "charlie@example.com"
        };

        var created = await _service.CreateMagicLinkAsync(user, "https://myapp.com/auth/callback");

        // First use
        var firstValidation = await _service.ValidateAndConsumeAsync(created.RawToken);
        firstValidation.Succeeded.ShouldBeTrue();

        // Second use of same token (replay attack)
        var secondValidation = await _service.ValidateAndConsumeAsync(created.RawToken);
        secondValidation.Succeeded.ShouldBeFalse();
        secondValidation.FailureReason!.ShouldContain("already consumed");
    }

    [Fact]
    public async Task ValidateAndConsume_Fails_WhenExpired()
    {
        var user = new KyrolusAuthUser
        {
            Id = "user-magic-4",
            Email = "dave@example.com"
        };

        // Negative lifetime -> immediately expired
        var created = await _service.CreateMagicLinkAsync(user, "https://myapp.com/auth/callback", TimeSpan.FromSeconds(-5));

        var validation = await _service.ValidateAndConsumeAsync(created.RawToken);

        validation.Succeeded.ShouldBeFalse();
        validation.FailureReason!.ShouldContain("expired");
    }

    [Fact]
    public void DiRegistration_AddKyrolusMagicLink_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMagicLink();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusMagicLinkStore>().ShouldNotBeNull();
        provider.GetService<IKyrolusMagicLinkService>().ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateAndConsume_Succeeds_WhenTokenContainsAccidentalWhitespace()
    {
        var user = new KyrolusAuthUser { Id = "user-trim", Email = "trim@example.com" };
        var created = await _service.CreateMagicLinkAsync(user, "https://myapp.com/callback");

        // User or email client adds whitespace/newlines:
        var validation = await _service.ValidateAndConsumeAsync($"  {created.RawToken} \r\n ");
        validation.Succeeded.ShouldBeTrue();
        validation.UserId.ShouldBe("user-trim");
    }

    [Fact]
    public async Task ValidateAndConsume_PermitsOnlyOneWinner_UnderHighConcurrency()
    {
        var user = new KyrolusAuthUser { Id = "user-concurrent", Email = "concurrent@example.com" };
        var created = await _service.CreateMagicLinkAsync(user, "https://myapp.com/callback");

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _service.ValidateAndConsumeAsync(created.RawToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        // Exactly 1 winner, 9 rejected replay attempts
        successCount.ShouldBe(1);
        failureCount.ShouldBe(9);
    }

    [Fact]
    public async Task PurgeExpiredTokens_RemovesOutdatedMagicLinks()
    {
        var user = new KyrolusAuthUser { Id = "user-purge", Email = "purge@example.com" };
        // Create expired token
        await _service.CreateMagicLinkAsync(user, "https://myapp.com/callback", TimeSpan.FromSeconds(-10));

        var purged = await _store.PurgeExpiredTokensAsync();
        purged.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CreateMagicLink_PreservesExistingQueryAndFragments()
    {
        var user = new KyrolusAuthUser { Id = "user-url", Email = "url@example.com" };
        var result = await _service.CreateMagicLinkAsync(user, "https://myapp.com/callback?lang=en#step2");

        result.MagicLinkUrl.ShouldContain("lang=en");
        result.MagicLinkUrl.ShouldContain("token=");
        result.MagicLinkUrl.ShouldContain("#step2");
    }

    [Fact]
    public async Task CreateMagicLink_Throws_WhenUserHasNoEmailOrUserName()
    {
        var emptyUser = new KyrolusAuthUser { Id = "user-empty", Email = null, UserName = "" };

        await Should.ThrowAsync<ArgumentException>(async () =>
            await _service.CreateMagicLinkAsync(emptyUser, "https://myapp.com/callback"));
    }

    [Theory]
    [InlineData("alice@example.com\r\nBcc:hacker@evil.com")]
    [InlineData("alice\n@example.com")]
    public async Task CreateMagicLink_Throws_WhenRecipientContainsCRLF(string maliciousEmail)
    {
        var crlfUser = new KyrolusAuthUser { Id = "user-crlf", Email = maliciousEmail };

        await Should.ThrowAsync<ArgumentException>(async () =>
            await _service.CreateMagicLinkAsync(crlfUser, "https://myapp.com/callback"));
    }

    [Fact]
    public async Task ValidateAndConsume_RejectsOversizedTokens()
    {
        var giantToken = new string('A', 1000);
        var result = await _service.ValidateAndConsumeAsync(giantToken);

        result.Succeeded.ShouldBeFalse();
        result.FailureReason!.ShouldContain("invalid or malformed");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<b>pwned</b>")]
    [InlineData("/relative/login")]
    public async Task CreateMagicLink_Throws_WhenCallbackUrlIsNotHttpOrHttps(string invalidUrl)
    {
        var user = new KyrolusAuthUser { Id = "user-url", Email = "valid@example.com" };

        await Should.ThrowAsync<ArgumentException>(async () =>
            await _service.CreateMagicLinkAsync(user, invalidUrl));
    }
}
