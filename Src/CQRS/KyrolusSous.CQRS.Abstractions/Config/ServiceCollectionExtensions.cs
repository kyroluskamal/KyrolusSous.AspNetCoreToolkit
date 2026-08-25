using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.CQRS.Abstractions.Config;

/// <summary>
/// Service collection extensions for registering CQRS behaviors and telemetry.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CQRS Performance &amp; OpenTelemetry pipeline behavior.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsTelemetry(
        this IServiceCollection services,
        Action<KyrolusCqrsPerformanceOptions>? configure = null)
    {
        var options = new KyrolusCqrsPerformanceOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusPerformanceAndTelemetryBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Concurrency Throttling pipeline behavior.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsThrottling(this IServiceCollection services)
    {
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusThrottlingBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Security &amp; Authorization pipeline behavior.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsAuthorization(this IServiceCollection services)
    {
        services.TryAddScoped<Security.ICurrentUserContext, Security.DefaultCurrentUserContext>();
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusAuthorizationBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Audit Trail pipeline behavior with the specified sink.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsAudit<TSink>(this IServiceCollection services)
        where TSink : class, Audit.IAuditSink
    {
        services.TryAddScoped<Audit.IAuditSink, TSink>();
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusAuditBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Audit Trail pipeline behavior with the default <see cref="Audit.LoggerAuditSink"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsAudit(this IServiceCollection services)
        => services.AddKyrolusCqrsAudit<Audit.LoggerAuditSink>();

    /// <summary>
    /// Registers the CQRS Transactional Outbox store and processor.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsOutbox<TStore>(this IServiceCollection services)
        where TStore : class, Outbox.IOutboxStore
    {
        services.TryAddSingleton<Outbox.IOutboxStore, TStore>();
        services.TryAddTransient<Outbox.KyrolusOutboxProcessor>();
        return services;
    }

    /// <summary>
    /// Registers the CQRS Transactional Outbox with the in-memory store.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsOutbox(this IServiceCollection services)
        => services.AddKyrolusCqrsOutbox<Outbox.InMemoryOutboxStore>();

    /// <summary>
    /// Registers the CQRS Read-Model Projection pipeline behavior.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsProjections(this IServiceCollection services)
    {
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusReadModelProjectionBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Real-Time Live Push pipeline behavior with the specified publisher.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsLivePush<TPublisher>(this IServiceCollection services)
        where TPublisher : class, LivePush.ILivePushPublisher
    {
        services.TryAddScoped<LivePush.ILivePushPublisher, TPublisher>();
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusLivePushBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Real-Time Live Push pipeline behavior with the default <see cref="LivePush.LoggerLivePushPublisher"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsLivePush(this IServiceCollection services)
        => services.AddKyrolusCqrsLivePush<LivePush.LoggerLivePushPublisher>();
}


