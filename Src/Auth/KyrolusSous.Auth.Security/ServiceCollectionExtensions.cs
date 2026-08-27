using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.Security;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds password policy checking and in-memory brute force protection guards.
    /// </summary>
    public static IServiceCollection AddKyrolusAuthSecurity(
        this IServiceCollection services,
        Action<KyrolusPasswordPolicyOptions>? configurePolicy = null,
        Action<KyrolusBruteForceOptions>? configureBruteForce = null)
    {
        var policyOptions = new KyrolusPasswordPolicyOptions();
        configurePolicy?.Invoke(policyOptions);
        services.TryAddSingleton(policyOptions);
        services.TryAddSingleton<IKyrolusPasswordPolicyChecker, KyrolusPasswordPolicyChecker>();

        var bruteForceOptions = new KyrolusBruteForceOptions();
        configureBruteForce?.Invoke(bruteForceOptions);
        services.TryAddSingleton(bruteForceOptions);
        services.TryAddSingleton<IKyrolusBruteForceGuard, KyrolusInMemoryBruteForceGuard>();

        return services;
    }
}
