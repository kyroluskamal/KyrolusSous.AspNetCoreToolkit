
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Repositories.EF.Abstractions.Query;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Repositories.EF.Runtime;

/// <summary>
/// Registers the runtime generic repository as an open-generic fallback.
/// Call this before registering any generated repositories so the generated ones override it.
/// </summary>
public static class KyrolusRuntimeRepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusRuntimeRepositories(this IServiceCollection services)
    {
        services.TryAddSingleton<KyrolusRepositoryCachePolicyRegistry>();
        services.TryAddSingleton<IKyrolusRepositoryCachePolicyProvider>(sp => sp.GetRequiredService<KyrolusRepositoryCachePolicyRegistry>());
        services.TryAddSingleton<IKyrolusRepositoryPolicyProvider>(KyrolusNoopRepositoryPolicyProvider.Instance);
        services.AddScoped(typeof(IKyrolusRepositoryAsync<,,>), typeof(KyrolusRepositoryAsync<,,>));
        return services;
    }

    public static IServiceCollection AddKyrolusRuntimeDefaults<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddKyrolusRuntimeRepositories();
        services.TryAddScoped(typeof(KyrolusSingleKeyRepositoryAsync<,,>));
        services.TryAddScoped(typeof(KyrolusCompositeKeyRepositoryAsync<,>));
        services.TryAddScoped(typeof(KyrolusSingleKeySoftDeleteRepositoryAsync<,,>));
        services.TryAddScoped(typeof(KyrolusCompositeKeySoftDeleteRepositoryAsync<,>));
        services.TryAddScoped(typeof(IKyrolusQueryHelper<>), typeof(RuntimeQueryHelper<>));
        services.TryAddScoped<IKyrolusUnitOfWork, KyrolusRuntimeUnitOfWork<TDbContext>>();
        return services;
    }
}
