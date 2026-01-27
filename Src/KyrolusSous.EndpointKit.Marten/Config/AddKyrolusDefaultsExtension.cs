using KyrolusSous.Caching.Abstractions;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Authorization;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using KyrolusSous.ExceptionHandling.ProblemDetails;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.EndpointKit.Marten.Config;

public static class AddKyrolusDefaultsExtension
{
    public static IServiceCollection AddKyrolusDefaults(this IServiceCollection services)
    {
        services.AddOptions<KyrolusEndpointKitOptions>();
        services.AddKyrolusProblemDetailsWriter();
        services.AddSingleton<KyrolusModuleBuilder>();
        services.AddScoped(typeof(IKyrolusApiConfig<>), typeof(KyrolusMartenApiConfig<>));
        services.AddScoped(typeof(IRouteMapper<,,>), typeof(KyrolusMartenRouteMapper<,,>));
        services.AddScoped<IKyrolusMapper, KyrolusMapper>();
        services.AddScoped(typeof(ICommandQueryHandler<,,>), typeof(DefaultCommandQueryHandler<,,>));
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.TryAddScoped<IKyrolusEndpointContext, KyrolusHttpEndpointContext>();
        services.TryAddScoped<ICacheKeyContext, KyrolusEndpointCacheKeyContext>();
        services.TryAddSingleton<ICacheProvider>(NullCacheProvider.Instance);
        services.TryAddSingleton<IKyrolusIdempotencyStore, KyrolusCacheIdempotencyStore>();
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
