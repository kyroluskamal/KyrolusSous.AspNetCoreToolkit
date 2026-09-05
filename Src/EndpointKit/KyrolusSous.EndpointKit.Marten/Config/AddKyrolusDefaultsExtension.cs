using KyrolusSous.Caching.Abstractions;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Authorization;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using KyrolusSous.ExceptionHandling.ProblemDetails;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;

using KyrolusSous.EndpointKit.Core.Middleware;

namespace KyrolusSous.EndpointKit.Marten.Config;

public static class AddKyrolusDefaultsExtension
{
    public static IServiceCollection AddKyrolusDefaults(this IServiceCollection services)
    {
        services.AddKyrolusCorrelation();
        services.AddOptions<KyrolusEndpointKitOptions>();
        services.AddKyrolusProblemDetailsWriter();
        services.AddSingleton<KyrolusModuleBuilder>();
        services.AddSingleton(typeof(IKyrolusApiConfig<>), typeof(KyrolusMartenApiConfig<>));
        services.AddSingleton(typeof(IKyrolusRouteMapper<,,>), typeof(KyrolusMartenRouteMapper<,,>));
        services.AddSingleton(typeof(IRouteMapper<,,>), typeof(KyrolusMartenRouteMapper<,,>));
        services.AddSingleton<IKyrolusMapper, KyrolusMapper>();
        services.AddScoped(typeof(IKyrolusCommandQueryHandler<,,>), typeof(DefaultCommandQueryHandler<,,>));
        services.AddScoped(typeof(ICommandQueryHandler<,,>), typeof(DefaultCommandQueryHandler<,,>));
        services.AddScoped(typeof(IKyrolusMartenCommandQueryHandler<,,>), typeof(DefaultCommandQueryHandler<,,>));
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.TryAddScoped<IKyrolusEndpointContext, KyrolusHttpEndpointContext>();
        services.TryAddScoped<IKyrolusCacheKeyContext, KyrolusEndpointCacheKeyContext>();
        services.TryAddSingleton<IKyrolusCacheProvider>(KyrolusNullCacheProvider.Instance);
        services.TryAddScoped<IKyrolusIdempotencyStore, KyrolusCacheIdempotencyStore>();
        services.TryAddSingleton(typeof(IKyrolusMartenAuthorizationProvider<>), typeof(KyrolusNoopMartenAuthorizationProvider<>));
        services.TryAddSingleton<KyrolusEndpointCachePolicyRegistry>();
        services.TryAddSingleton<IKyrolusEndpointCachePolicyProvider>(sp => sp.GetRequiredService<KyrolusEndpointCachePolicyRegistry>());
        services.Configure<OpenApiOptions>(options =>
        {
            options.AddOperationTransformer<KyrolusOpenApiOperationTransformer>();
        });
        return services;
    }
}
