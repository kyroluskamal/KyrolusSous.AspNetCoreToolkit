namespace KyrolusSous.ExceptionHandling.Runtime;

/// <summary>
/// Provides extension methods for registering and configuring Kyrolus Exception Handling services and middleware.
/// </summary>
public static class ExceptionHandlingExtension
{
    /// <summary>
    /// Registers core Kyrolus Exception Handling services, translators, sanitizers, and mappers into the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure <see cref="KyrolusExceptionHandlingOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusExceptionHandling(this IServiceCollection services, Action<KyrolusExceptionHandlingOptions>? configure = null)
    {
        if (configure is not null)
        {
            var options = new KyrolusExceptionHandlingOptions();
            configure(options);
            if (options.EnforceErrorCodeRegistry)
                KyrolusErrorCodeRegistry.EnableStrictMode();
            services.Configure(configure);
        }

        services.TryAddSingleton<KyrolusHttpErrorContextFactory>();
        services.TryAddSingleton<KyrolusExceptionMappingService>();

        // No IKyrolusLocalizer is registered by default - every consumer here takes it as an
        // optional dependency, so leaving it unregistered means "no localization" with zero setup.
        // Register one via KyrolusSous.Localization.Json's AddKyrolusJsonLocalization /
        // AddKyrolusDictionaryLocalization, or KyrolusSous.Localization.StringLocalizer's
        // AddKyrolusStringLocalizerLocalization<TResource> for an ASP.NET Core IStringLocalizer<T>-backed one.
        services.TryAddSingleton<IKyrolusErrorMetadataSanitizer, KyrolusDefaultErrorMetadataSanitizer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusDomainExceptionMapper>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusFrameworkExceptionMapper>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusDefaultExceptionMapper>());

        services.TryAddSingleton<IKyrolusErrorResponseWriter, KyrolusJsonErrorResponseWriter>();
        services.TryAddSingleton<KyrolusExceptionHandlingDependencies>();
        services.TryAddSingleton<KyrolusExceptionTranslator>();
        services.TryAddSingleton<KyrolusExceptionFilter>();

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core IExceptionHandler implementations for built-in .NET exceptions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusBuiltInExceptionHandlers(this IServiceCollection services)
    {
        services.AddExceptionHandler<CultureNotFoundExceptionHandler>();
        services.AddExceptionHandler<JsonExceptionHandler>();
        services.AddExceptionHandler<ArgumentExceptionHandler>();
        services.AddExceptionHandler<SocketExceptionHandler>();
        services.AddExceptionHandler<HttpRequestExceptionHandler>();
        services.AddExceptionHandler<TimeoutExceptionHandler>();
        services.AddExceptionHandler<NotFoundExceptionHandler>();
        services.AddExceptionHandler<UnauthorizedExceptionHandler>();
        services.AddExceptionHandler<SslAuthenticationExceptionHandler>();
        services.AddExceptionHandler<GeneralExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Adds the <see cref="ExceptionHandlingMiddleware"/> to the ASP.NET Core request pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseKyrolusExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
