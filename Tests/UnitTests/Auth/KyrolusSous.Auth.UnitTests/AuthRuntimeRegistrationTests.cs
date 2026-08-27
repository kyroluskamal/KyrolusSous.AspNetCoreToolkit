using System.Security.Cryptography;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

public sealed class AuthRuntimeRegistrationTests
{
    [Fact]
    public void AddKyrolusAuthCore_registers_the_defaults()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusAuthCore();
        services.AddKyrolusInMemoryAuthUserStore();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IKyrolusPasswordHasher>()
            .ShouldBeOfType<KyrolusPbkdf2PasswordHasher>();
        provider.GetRequiredService<IKyrolusClaimsPrincipalFactory>()
            .ShouldBeOfType<KyrolusClaimsPrincipalFactory>();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IKyrolusUserAuthenticator>()
            .ShouldBeOfType<KyrolusUserAuthenticator>();
        scope.ServiceProvider.GetRequiredService<IKyrolusExternalLoginHandler>()
            .ShouldBeOfType<KyrolusExternalLoginHandler>();
    }

    [Fact]
    public void An_application_supplied_implementation_wins()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IKyrolusPasswordHasher, StubPasswordHasher>();
        services.AddKyrolusAuthCore();

        using var provider = services.BuildServiceProvider();

        // Every default goes in through TryAdd, so a house implementation registered first is
        // never displaced.
        provider.GetRequiredService<IKyrolusPasswordHasher>().ShouldBeOfType<StubPasswordHasher>();
    }

    [Fact]
    public void The_in_memory_store_serves_both_the_store_and_lockout_roles()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusInMemoryAuthUserStore(store =>
            store.Add(new KyrolusAuthUser { UserName = "seeded" }));

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IKyrolusAuthUserStore>();
        var lockout = provider.GetRequiredService<IKyrolusAuthUserLockoutStore>();

        store.ShouldBeSameAs(lockout);
        ((KyrolusInMemoryAuthUserStore)store).Users.ShouldContain(u => u.UserName == "seeded");
    }

    [Fact]
    public void AddKyrolusAuthUserStore_registers_the_application_store()
    {
        var services = new ServiceCollection();
        services.AddKyrolusAuthUserStore<KyrolusInMemoryAuthUserStore>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IKyrolusAuthUserStore>()
            .ShouldBeOfType<KyrolusInMemoryAuthUserStore>();
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(9_999)]
    public void A_weak_iteration_count_fails_validation(int iterations)
    {
        var result = new KyrolusAuthOptionsValidator()
            .Validate(null, new KyrolusAuthOptions { Pbkdf2Iterations = iterations });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(KyrolusAuthOptions.Pbkdf2Iterations));
    }

    [Fact]
    public void An_unrepresentable_hash_algorithm_fails_validation()
    {
        var result = new KyrolusAuthOptionsValidator().Validate(
            null,
            new KyrolusAuthOptions { Pbkdf2HashAlgorithm = HashAlgorithmName.MD5 });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(KyrolusAuthOptions.Pbkdf2HashAlgorithm));
    }

    [Fact]
    public void The_defaults_pass_validation()
    {
        new KyrolusAuthOptionsValidator().Validate(null, new KyrolusAuthOptions())
            .Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_reports_every_failure_at_once()
    {
        var result = new KyrolusAuthOptionsValidator().Validate(null, new KyrolusAuthOptions
        {
            Pbkdf2Iterations = 1,
            SaltSizeInBytes = 4,
            KeySizeInBytes = 8,
            MaxFailedAccessAttempts = -1,
            LockoutDuration = TimeSpan.FromMinutes(-1),
        });

        result.Failures.ShouldNotBeNull();
        result.Failures.Count().ShouldBe(5);
    }

    private sealed class StubPasswordHasher : IKyrolusPasswordHasher
    {
        public string Hash(string password) => password;

        public KyrolusPasswordVerificationResult Verify(string hashedPassword, string providedPassword)
            => hashedPassword == providedPassword
                ? KyrolusPasswordVerificationResult.Success
                : KyrolusPasswordVerificationResult.Failed;
    }
}
