using System.Diagnostics.CodeAnalysis;
using KyrolusSous.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Validation.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusValidationRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<IKyrolusValidationEngine, KyrolusValidationEngine>();
        services.TryAddSingleton<IKyrolusValidationProfileProvider, KyrolusValidationProfileProvider>();
        services.TryAddSingleton<IKyrolusValidationCacheStore, KyrolusValidationMemoryCacheStore>();
        services.TryAddSingleton<IKyrolusValidationCacheKeyProvider, KyrolusValidationCacheKeyProvider>();
        services.TryAddSingleton<IKyrolusValidationMetrics, KyrolusNoopValidationMetrics>();
        services.TryAddSingleton<IKyrolusValidationTracer, KyrolusNoopValidationTracer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusValidationHook, KyrolusValidationMetricsHook>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusValidationHook, KyrolusValidationTracingHook>());
        return services;
    }

    public static IServiceCollection AddKyrolusValidationProfile(
        this IServiceCollection services,
        KyrolusValidationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        AddKyrolusValidationRuntime(services);
        services.AddSingleton(profile);
        return services;
    }

    [RequiresUnreferencedCode("Uses reflection to scan for validators. This is not AOT-friendly.")]
    public static IServiceCollection AddKyrolusValidationRuntimeScanning(
        this IServiceCollection services,
        params System.Reflection.Assembly[] assemblies)
    {
        AddKyrolusValidationRuntime(services);
        RegisterValidators(services, assemblies);
        return services;
    }

    [RequiresUnreferencedCode("Uses reflection to scan for validators. This is not AOT-friendly.")]
    private static void RegisterValidators(IServiceCollection services, System.Reflection.Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            return;
        }

        foreach (var type in assemblies.SelectMany(static assembly => assembly.GetTypes()).Where(IsConcreteType))
        {
            foreach (var iface in GetValidatorInterfaces(type))
            {
                services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
            }
        }
    }

    private static bool IsConcreteType(Type type)
        => !type.IsAbstract && !type.IsInterface;

    private static IEnumerable<Type> GetValidatorInterfaces(Type type)
    {
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IKyrolusRequestValidator<>))
            {
                yield return iface;
            }
        }
    }
}
