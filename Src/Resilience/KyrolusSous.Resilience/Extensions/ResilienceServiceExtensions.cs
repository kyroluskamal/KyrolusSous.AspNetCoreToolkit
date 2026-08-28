using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;

namespace KyrolusSous.Resilience;

public static class ResilienceServiceExtensions
{
    public static WebApplicationBuilder AddKyrolusResilience(
        this WebApplicationBuilder builder,
        Action<KyrolusResilienceOptions>? configureOptions = null)
    {
        var configSection = builder.Configuration.GetSection("KyrolusResilience");
        builder.Services.AddKyrolusResilience(configSection, configureOptions);
        return builder;
    }

    public static IServiceCollection AddKyrolusResilience(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KyrolusResilienceOptions>? configureOptions = null)
    {
        services.Configure<KyrolusResilienceOptions>(configuration);
        if (configureOptions is not null)
        {
            services.PostConfigure(configureOptions);
        }
        return RegisterResilienceServices(services);
    }

    public static IServiceCollection AddKyrolusResilience(
        this IServiceCollection services,
        Action<KyrolusResilienceOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        return RegisterResilienceServices(services);
    }

    public static IServiceCollection AddKyrolusCustomResiliencePipeline(
        this IServiceCollection services,
        string name,
        Action<ResiliencePipelineBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSingleton<IKyrolusCustomPipelineConfigurator>(
            new KyrolusDelegateCustomPipelineConfigurator(name, configure));
        return services;
    }

    public static IServiceCollection AddTransientExceptionEvaluator<TEvaluator>(this IServiceCollection services)
        where TEvaluator : class, IKyrolusTransientExceptionEvaluator
    {
        services.AddSingleton<IKyrolusTransientExceptionEvaluator, TEvaluator>();
        return services;
    }

    public static IServiceCollection AddResilienceAlertHandler<THandler>(this IServiceCollection services)
        where THandler : class, IKyrolusResilienceAlertHandler
    {
        services.AddSingleton<IKyrolusResilienceAlertHandler, THandler>();
        return services;
    }

    /// <summary>
    /// Registers a service implementation wrapped with a resilient dispatch proxy decorator.
    /// </summary>
    public static IServiceCollection AddResilientDecorated<TInterface, TImplementation>(
        this IServiceCollection services,
        string pipelineName = "default",
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.Add(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), lifetime));
        services.Add(new ServiceDescriptor(typeof(TInterface), sp =>
        {
            var target = sp.GetRequiredService<TImplementation>();
            var pipelineProvider = sp.GetRequiredService<IKyrolusResiliencePipelineProvider>();
            return KyrolusResilienceProxy<TInterface>.Create(target, pipelineProvider, pipelineName);
        }, lifetime));

        return services;
    }

    /// <summary>
    /// Registers a declarative fallback for a specific pipeline name and result type.
    /// </summary>
    public static IServiceCollection AddResilienceFallback<TResult>(
        this IServiceCollection services,
        string pipelineName,
        Func<Exception, CancellationToken, ValueTask<TResult>> fallback)
    {
        services.AddSingleton<IKyrolusFallbackRegistration>(
            new KyrolusFallbackRegistration<TResult>(pipelineName, fallback));
        return services;
    }

    /// <summary>
    /// Attaches a named Kyrolus resilience delegating handler to an HttpClientBuilder.
    /// </summary>
    public static IHttpClientBuilder AddKyrolusResilienceHandler(
        this IHttpClientBuilder builder,
        string pipelineName = "default")
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddHttpMessageHandler(sp =>
        {
            var pipelineProvider = sp.GetRequiredService<IKyrolusResiliencePipelineProvider>();
            return new KyrolusResilienceDelegatingHandler(pipelineProvider, pipelineName);
        });
    }

    public static IServiceCollection AddResilienceMediatorBehavior(this IServiceCollection services)
    {
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusResiliencePipelineBehavior<,>));
        return services;
    }

    public static IHealthChecksBuilder AddResilienceCircuitBreakerHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "resilience_circuit_breaker",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<KyrolusResilienceCircuitBreakerHealthCheck>(
            name,
            failureStatus ?? HealthStatus.Degraded,
            tags ?? ["resilience", "circuit_breaker", "ready"]);
    }

    public static async ValueTask ExecuteWithResilienceAsync(
        this IKyrolusResiliencePipelineProvider provider,
        Func<CancellationToken, ValueTask> action,
        string pipelineName = "default",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(action);

        var pipeline = provider.GetPipeline(pipelineName);
        await pipeline.ExecuteAsync(async ct => await action(ct), cancellationToken);
    }

    public static async ValueTask<TResult> ExecuteWithResilienceAsync<TResult>(
        this IKyrolusResiliencePipelineProvider provider,
        Func<CancellationToken, ValueTask<TResult>> action,
        string pipelineName = "default",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(action);

        var pipeline = provider.GetPipeline<TResult>(pipelineName);
        return await pipeline.ExecuteAsync(async ct => await action(ct), cancellationToken);
    }

    private static IServiceCollection RegisterResilienceServices(IServiceCollection services)
    {
        services.AddOptions<KyrolusResilienceOptions>();
        services.TryAddSingleton<IKyrolusCircuitBreakerStateStore, KyrolusInMemoryCircuitBreakerStateStore>();
        services.TryAddSingleton<IKyrolusResilienceAlertSink, KyrolusCompositeResilienceAlertSink>();
        services.TryAddSingleton<IKyrolusCircuitBreakerObserver, KyrolusCircuitBreakerObserver>();
        services.TryAddSingleton<IKyrolusTransientExceptionEvaluator, KyrolusDefaultTransientExceptionEvaluator>();
        services.TryAddSingleton<IKyrolusFallbackRegistry, KyrolusFallbackRegistry>();
        services.TryAddSingleton<IKyrolusAdaptiveConcurrencyLimiter>(_ => new KyrolusAdaptiveConcurrencyLimiter());
        services.TryAddSingleton<IKyrolusSingleFlight, KyrolusSingleFlight>();
        services.TryAddSingleton<IKyrolusChaosEngine, KyrolusChaosEngine>();
        services.TryAddSingleton<IKyrolusPartitionedRateLimiter, KyrolusPartitionedRateLimiter>();
        services.TryAddSingleton<IKyrolusPriorityLoadShedder, KyrolusPriorityLoadShedder>();
        services.TryAddSingleton<IKyrolusAdaptiveTimeoutEstimator, KyrolusAdaptiveTimeoutEstimator>();
        services.TryAddSingleton<IKyrolusResilienceQuarantine, KyrolusResilienceQuarantine>();
        services.TryAddSingleton<IKyrolusResiliencePipelineComposer, KyrolusResiliencePipelineComposer>();
        services.TryAddSingleton<IKyrolusResiliencePipelineProvider, KyrolusResiliencePipelineProvider>();
        return services;
    }
}
