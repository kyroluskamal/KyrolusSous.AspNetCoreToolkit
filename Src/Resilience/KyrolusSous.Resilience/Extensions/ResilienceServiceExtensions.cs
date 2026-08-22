using KyrolusSous.Mediator.Abstractions.Interfaces;

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
            new DelegateCustomPipelineConfigurator(name, configure));
        return services;
    }

    public static IServiceCollection AddResilienceMediatorBehavior(this IServiceCollection services)
    {
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(ResiliencePipelineBehavior<,>));
        return services;
    }

    public static IHealthChecksBuilder AddResilienceCircuitBreakerHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "resilience_circuit_breaker",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<ResilienceCircuitBreakerHealthCheck>(
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
        services.AddSingleton<IKyrolusResiliencePipelineProvider, KyrolusResiliencePipelineProvider>();
        return services;
    }
}
