using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KyrolusSous.Repositories.EF.Runtime.Observability;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusRepositoryTelemetryObserver(
        this IServiceCollection services,
        Action<KyrolusRepositoryTelemetryObserverOptions>? configure = null)
    {
        services.TryAddSingleton(sp =>
        {
            var options = new KyrolusRepositoryTelemetryObserverOptions();
            configure?.Invoke(options);
            return options;
        });

        services.AddSingleton<IKyrolusRepositoryObserver, KyrolusRepositoryTelemetryObserver>();
        return services;
    }

    public static IServiceCollection AddKyrolusRepositoryOpenTelemetry(
        this IServiceCollection services,
        string? serviceName = null,
        bool enableOtlpExporter = true,
        bool enableConsoleExporter = false,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        var resolvedServiceName = string.IsNullOrWhiteSpace(serviceName)
            ? "KyrolusSous.Repositories.EF"
            : serviceName!;

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(resolvedServiceName))
                    .AddSource(KyrolusRepositoryInstrumentation.ActivitySourceName);

                if (enableOtlpExporter)
                {
                    builder.AddOtlpExporter();
                }

                if (enableConsoleExporter)
                {
                    builder.AddConsoleExporter();
                }

                configureTracing?.Invoke(builder);
            })
            .WithMetrics(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(resolvedServiceName))
                    .AddMeter(KyrolusRepositoryInstrumentation.MeterName);

                if (enableOtlpExporter)
                {
                    builder.AddOtlpExporter();
                }

                if (enableConsoleExporter)
                {
                    builder.AddConsoleExporter();
                }

                configureMetrics?.Invoke(builder);
            });

        return services;
    }
}
