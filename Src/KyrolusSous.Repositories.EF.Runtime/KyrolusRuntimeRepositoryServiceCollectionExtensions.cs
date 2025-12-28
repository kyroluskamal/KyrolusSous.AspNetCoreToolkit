using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Repositories.EF.Runtime;

/// <summary>
/// Registers the runtime generic repository as an open-generic fallback.
/// Call this before registering any generated repositories so the generated ones override it.
/// </summary>
public static class KyrolusRuntimeRepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusRuntimeRepositories(this IServiceCollection services)
    {
        services.AddScoped(typeof(IKyrolusRepositoryAsync<,,>), typeof(KyrolusRepositoryAsync<,,>));
        return services;
    }
}
