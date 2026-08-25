using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.CQRS.Caching;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCqrsCaching(this IServiceCollection services)
    {
        services.TryAddSingleton<IKyrolusCacheKeyProvider, KyrolusDefaultCacheKeyProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusIdempotencyBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusQueryCachingBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusCommandCacheInvalidationBehavior<,>)));
        return services;
    }
}
