using KyrolusSous.Auth.ApiKey;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.ApiKey.UnitTests;

public class ApiKeyTests
{
    private readonly KyrolusApiKeyGenerator _generator = new();

    [Fact(DisplayName = "Generate Key Creates Key With Prefix")]
    public void GenerateKey_CreatesKeyWithPrefix()
    {
        var key = _generator.GenerateKey("test_live_");

        key.ShouldNotBeNullOrWhiteSpace();
        key.ShouldStartWith("test_live_");
        key.Length.ShouldBeGreaterThan(20);
    }

    [Theory(DisplayName = "Generate Key Rejects Invalid Prefix")]
    [InlineData("has space_")]
    [InlineData("crlf\r\n_")]
    [InlineData("this_prefix_is_way_too_long_and_exceeds_thirty_two_characters_limit_")]
    public void GenerateKey_RejectsInvalidPrefix(string invalidPrefix)
    {
        Should.Throw<ArgumentException>(() => _generator.GenerateKey(invalidPrefix));
    }

    [Fact(DisplayName = "Hash Key Is Deterministic")]
    public void HashKey_IsDeterministic()
    {
        var key = "test-key-12345";
        var hash1 = _generator.HashKey(key);
        var hash2 = _generator.HashKey(key);

        hash1.ShouldNotBeNullOrWhiteSpace();
        hash1.ShouldBe(hash2);
        hash1.Length.ShouldBe(64); // SHA-256 hex length
    }

    [Fact(DisplayName = "Validator Validates Successfully")]
    public async Task Validator_ValidatesSuccessfully()
    {
        var validator = new TestApiKeyValidator();
        var result = await validator.ValidateAsync("valid-key-abc");

        result.Succeeded.ShouldBeTrue();
        result.ApiKey.ShouldNotBeNull();
        result.ApiKey.OwnerId.ShouldBe("partner-42");
        result.ApiKey.Scopes.ShouldContain("orders.read");
    }

    [Fact(DisplayName = "Validator Rejects Invalid Key")]
    public async Task Validator_RejectsInvalidKey()
    {
        var validator = new TestApiKeyValidator();
        var result = await validator.ValidateAsync("invalid-key-xyz");

        result.Succeeded.ShouldBeFalse();
        result.ApiKey.ShouldBeNull();
        result.FailureReason.ShouldBe("Key not found");
    }

    [Fact(DisplayName = "Di Registration Registers Api Key Services")]
    public void DiRegistration_RegistersApiKeyServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusApiKeyAuth(options =>
        {
            options.HeaderName = "X-Custom-Key";
        });
        services.AddKyrolusApiKeyValidator<TestApiKeyValidator>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusApiKeyGenerator>().ShouldNotBeNull();
        provider.GetService<IKyrolusApiKeyValidator>().ShouldNotBeNull();
    }

    private sealed class TestApiKeyValidator : IKyrolusApiKeyValidator
    {
        public Task<KyrolusApiKeyValidationResult> ValidateAsync(string providedKey, CancellationToken cancellationToken = default)
        {
            if (providedKey == "valid-key-abc")
            {
                var apiKey = new KyrolusApiKey(
                    KeyHash: "hash-123",
                    OwnerId: "partner-42",
                    OwnerName: "Acme Corp",
                    Scopes: ["orders.read", "orders.create"],
                    Roles: ["Partner"]);

                return Task.FromResult(KyrolusApiKeyValidationResult.Success(apiKey));
            }

            return Task.FromResult(KyrolusApiKeyValidationResult.Failed("Key not found"));
        }
    }

    [Fact(DisplayName = "Handler Rejects Multiple Api Key Headers")]
    public async Task Handler_RejectsMultipleApiKeyHeaders()
    {
        var options = new KyrolusApiKeyAuthenticationOptions { HeaderName = "X-Api-Key" };
        var optionsMonitor = new TestOptionsMonitor<KyrolusApiKeyAuthenticationOptions>(options);
        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        var encoder = System.Text.Encodings.Web.UrlEncoder.Default;
        var validator = new TestApiKeyValidator();

        var handler = new KyrolusApiKeyAuthenticationHandler(optionsMonitor, loggerFactory, encoder, validator);

        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = new Microsoft.Extensions.Primitives.StringValues(["key-one", "key-two"]);

        await handler.InitializeAsync(new Microsoft.AspNetCore.Authentication.AuthenticationScheme("ApiKey", "ApiKey", typeof(KyrolusApiKeyAuthenticationHandler)), context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldContain("Multiple API key headers are not permitted");
    }

    [Fact(DisplayName = "Handler Rejects Inactive Api Key Even If Validator Succeeds")]
    public async Task Handler_RejectsInactiveApiKey_EvenIfValidatorSucceeds()
    {
        var options = new KyrolusApiKeyAuthenticationOptions { HeaderName = "X-Api-Key" };
        var optionsMonitor = new TestOptionsMonitor<KyrolusApiKeyAuthenticationOptions>(options);
        var validator = new ConfigurableApiKeyValidator(new KyrolusApiKey(
            KeyHash: "hash-inactive",
            OwnerId: "partner-inactive",
            OwnerName: "Revoked Corp",
            IsActive: false));

        var handler = new KyrolusApiKeyAuthenticationHandler(
            optionsMonitor,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default,
            validator);

        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "some-inactive-key";

        await handler.InitializeAsync(new Microsoft.AspNetCore.Authentication.AuthenticationScheme("ApiKey", "ApiKey", typeof(KyrolusApiKeyAuthenticationHandler)), context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldContain("inactive or revoked");
    }

    [Fact(DisplayName = "Handler Rejects Expired Api Key Even If Validator Succeeds")]
    public async Task Handler_RejectsExpiredApiKey_EvenIfValidatorSucceeds()
    {
        var options = new KyrolusApiKeyAuthenticationOptions { HeaderName = "X-Api-Key" };
        var optionsMonitor = new TestOptionsMonitor<KyrolusApiKeyAuthenticationOptions>(options);
        var validator = new ConfigurableApiKeyValidator(new KyrolusApiKey(
            KeyHash: "hash-expired",
            OwnerId: "partner-expired",
            OwnerName: "Expired Corp",
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5),
            IsActive: true));

        var handler = new KyrolusApiKeyAuthenticationHandler(
            optionsMonitor,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default,
            validator);

        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "some-expired-key";

        await handler.InitializeAsync(new Microsoft.AspNetCore.Authentication.AuthenticationScheme("ApiKey", "ApiKey", typeof(KyrolusApiKeyAuthenticationHandler)), context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure.Message.ShouldContain("expired");
    }

    [Fact(DisplayName = "Handler Falls Back To Default Header Name When Configured Header Name Is Null Or Whitespace")]
    public async Task Handler_FallsBackToDefaultHeaderName_WhenConfiguredHeaderNameIsNullOrWhitespace()
    {
        var validKey = new KyrolusApiKey(
            KeyHash: "hash-valid",
            OwnerId: "partner-1",
            OwnerName: "Valid Corp",
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddDays(1),
            IsActive: true);
        var validator = new ConfigurableApiKeyValidator(validKey);
        var options = new KyrolusApiKeyAuthenticationOptions
        {
            HeaderName = "   "
        };
        var monitor = new TestOptionsMonitor<KyrolusApiKeyAuthenticationOptions>(options);
        var handler = new KyrolusApiKeyAuthenticationHandler(
            monitor,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default,
            validator);

        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "some-key";

        await handler.InitializeAsync(new Microsoft.AspNetCore.Authentication.AuthenticationScheme("ApiKey", "ApiKey", typeof(KyrolusApiKeyAuthenticationHandler)), context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeTrue();
    }

    [Fact(DisplayName = "Generator Hash Key Throws For Oversized Key")]
    public void Generator_HashKey_Throws_ForOversizedKey()
    {
        var generator = new KyrolusApiKeyGenerator();
        var giantKey = new string('x', 600);

        Should.Throw<ArgumentException>(() =>
            generator.HashKey(giantKey));
    }

    private sealed class ConfigurableApiKeyValidator(IKyrolusApiKey apiKey) : IKyrolusApiKeyValidator
    {
        public Task<KyrolusApiKeyValidationResult> ValidateAsync(string providedKey, CancellationToken cancellationToken = default)
            => Task.FromResult(KyrolusApiKeyValidationResult.Success(apiKey));
    }

    private sealed class TestOptionsMonitor<T>(T current) : Microsoft.Extensions.Options.IOptionsMonitor<T>
    {
        public T CurrentValue => current;
        public T Get(string? name) => current;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
