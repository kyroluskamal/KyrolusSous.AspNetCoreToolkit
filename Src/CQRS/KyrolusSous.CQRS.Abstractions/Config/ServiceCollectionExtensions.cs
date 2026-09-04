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
    /// <remarks>
    /// This overload does not register an <see cref="Security.IKyrolusAuthorizationPolicyEvaluator"/>.
    /// A request that names a <c>Policy</c> will fail closed with a configuration error until one is
    /// registered - via the other overload, or directly with the container. Roles and permissions work
    /// without it.
    /// </remarks>
    public static IServiceCollection AddKyrolusCqrsAuthorization(this IServiceCollection services)
    {
        services.TryAddScoped<Security.IKyrolusCurrentUserContext, Security.KyrolusDefaultCurrentUserContext>();
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusAuthorizationBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Security &amp; Authorization pipeline behavior together with a named-policy
    /// evaluator (for example, a bridge to ASP.NET Core's <c>IAuthorizationService</c>).
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsAuthorization<TPolicyEvaluator>(this IServiceCollection services)
        where TPolicyEvaluator : class, Security.IKyrolusAuthorizationPolicyEvaluator
    {
        services.TryAddScoped<Security.IKyrolusAuthorizationPolicyEvaluator, TPolicyEvaluator>();
        return services.AddKyrolusCqrsAuthorization();
    }

    /// <summary>
    /// Registers the CQRS Audit Trail pipeline behavior with the specified sink.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureSanitization">
    /// Optional callback to extend the built-in sensitive-keyword list used to redact audit payloads
    /// (see <see cref="Audit.KyrolusAuditSanitizationOptions"/>). To source the list from
    /// <c>appsettings.json</c> instead of hard-coding it, bind the options inside the callback:
    /// <c>configureSanitization: opts =&gt; configuration.GetSection("Kyrolus:Cqrs:Audit:Sanitization").Bind(opts)</c>.
    /// </param>
    public static IServiceCollection AddKyrolusCqrsAudit<TSink>(
        this IServiceCollection services,
        Action<Audit.KyrolusAuditSanitizationOptions>? configureSanitization = null)
        where TSink : class, Audit.IKyrolusAuditSink
    {
        services.TryAddScoped<Audit.IKyrolusAuditSink, TSink>();

        if (configureSanitization is not null)
        {
            var sanitizationOptions = new Audit.KyrolusAuditSanitizationOptions();
            configureSanitization(sanitizationOptions);
            services.TryAddSingleton(sanitizationOptions);
        }

        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusAuditBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Audit Trail pipeline behavior with the default <see cref="Audit.LoggerAuditSink"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureSanitization">See <see cref="AddKyrolusCqrsAudit{TSink}"/>.</param>
    public static IServiceCollection AddKyrolusCqrsAudit(
        this IServiceCollection services,
        Action<Audit.KyrolusAuditSanitizationOptions>? configureSanitization = null)
        => services.AddKyrolusCqrsAudit<Audit.LoggerAuditSink>(configureSanitization);

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
        => services.AddKyrolusCqrsOutbox<Outbox.KyrolusInMemoryOutboxStore>();

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
        where TPublisher : class, LivePush.IKyrolusLivePushPublisher
    {
        services.TryAddScoped<LivePush.IKyrolusLivePushPublisher, TPublisher>();
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusLivePushBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Real-Time Live Push pipeline behavior with the default <see cref="LivePush.KyrolusLoggerLivePushPublisher"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsLivePush(this IServiceCollection services)
        => services.AddKyrolusCqrsLivePush<LivePush.KyrolusLoggerLivePushPublisher>();

    /// <summary>
    /// Registers the CQRS Multi-Tenancy guard, which rejects a request implementing
    /// <see cref="Interfaces.IKyrolusTenantScopedRequest"/> whose <c>TenantId</c> does not match the current
    /// user's tenant.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsTenantScoping(this IServiceCollection services)
    {
        services.TryAddScoped<Security.IKyrolusCurrentUserContext, Security.KyrolusDefaultCurrentUserContext>();
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusTenantScopingBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers the CQRS Property Allow-List guard, which rejects a Patch/BulkPatch/ExecuteUpdate
    /// request that names a property outside its own declared
    /// <see cref="Interfaces.IKyrolusPropertyUpdateRequest.AllowedProperties"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsPropertyAllowList(this IServiceCollection services)
    {
        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusPropertyAllowListBehavior<,>));
        return services;
    }
}


