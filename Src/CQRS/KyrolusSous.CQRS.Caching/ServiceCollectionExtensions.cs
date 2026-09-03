using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.CQRS.Caching;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCqrsCaching(this IServiceCollection services)
    {
        // Registered here (not just by AddKyrolusCqrsAuthorization) so KyrolusQueryCachingBehavior
        // can always scope cache keys by tenant/user, even in an app that uses caching but never
        // registers the authorization behavior.
        services.TryAddScoped<IKyrolusCurrentUserContext, KyrolusDefaultCurrentUserContext>();
        services.TryAddSingleton<IKyrolusCacheKeyProvider, KyrolusDefaultCacheKeyProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusIdempotencyBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusQueryCachingBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusCommandCacheInvalidationBehavior<,>)));
        return services;
    }
}
