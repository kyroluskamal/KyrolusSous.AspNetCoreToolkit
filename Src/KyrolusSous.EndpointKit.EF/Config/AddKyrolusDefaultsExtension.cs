using KyrolusSous.Caching.Abstractions;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Authorization;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;
using KyrolusSous.ExceptionHandling.ProblemDetails;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.EndpointKit.EF.Config;

public static class AddKyrolusDefaultsExtension
{
    public static IServiceCollection AddKyrolusDefaults(this IServiceCollection services)
    {
        services.AddOptions<KyrolusEndpointKitOptions>();
        services.AddKyrolusProblemDetailsWriter();
        services.AddSingleton<KyrolusModuleBuilder>();
        services.AddSingleton(typeof(IKyrolusApiConfig<>), typeof(KyrolusEfApiConfig<>));
        services.AddSingleton(typeof(IRouteMapper<,,>), typeof(KyrolusEfRouteMapper<,,>));
        services.AddSingleton<IKyrolusMapper, KyrolusMapper>();
        services.AddScoped(typeof(ICommandQueryHandler<,,>), typeof(DefaultCommandQueryHandler<,,>));
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.TryAddScoped<IKyrolusEndpointContext, KyrolusHttpEndpointContext>();
        services.TryAddScoped<ICacheKeyContext, KyrolusEndpointCacheKeyContext>();
        services.TryAddSingleton<ICacheProvider>(NullCacheProvider.Instance);
        services.TryAddScoped<IKyrolusIdempotencyStore, KyrolusCacheIdempotencyStore>();
        services.TryAddSingleton(typeof(IKyrolusEfAuthorizationProvider<>), typeof(KyrolusNoopEfAuthorizationProvider<>));
        services.TryAddSingleton<KyrolusEndpointCachePolicyRegistry>();
        services.TryAddSingleton<IKyrolusEndpointCachePolicyProvider>(sp => sp.GetRequiredService<KyrolusEndpointCachePolicyRegistry>());
        services.Configure<OpenApiOptions>(options =>
        {
            options.AddOperationTransformer<KyrolusOpenApiOperationTransformer>();
        });
        return services;
    }
}
