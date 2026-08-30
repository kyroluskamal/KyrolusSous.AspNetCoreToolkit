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

        services.TryAddSingleton<IKyrolusErrorLocalizer, KyrolusNullErrorLocalizer>();
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

    /// <summary>
    /// Configures resource-based localization for error codes using <see cref="IStringLocalizer{TResource}"/>.
    /// </summary>
    /// <typeparam name="TResource">The marker resource class.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusExceptionHandlingLocalization<TResource>(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusErrorLocalizer>(sp =>
        {
            var localizer = sp.GetRequiredService<IStringLocalizer<TResource>>();
            return new KyrolusStringLocalizerErrorLocalizer(localizer);
        });

        return services;
    }

    /// <summary>
    /// Configures in-memory dictionary-based localization for error codes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="translations">A dictionary mapping error codes to localized messages.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusExceptionHandlingLocalization(this IServiceCollection services, IReadOnlyDictionary<string, string> translations)
    {
        services.AddSingleton<IKyrolusErrorLocalizer>(_ => new KyrolusDictionaryErrorLocalizer(translations));
        return services;
    }

    /// <summary>
    /// Configures JSON directory-based error localization, scanning and loading all translation files
    /// (e.g., "errors.ar.json", "errors.ar-EG.json", "errors.json") into a unified in-memory dictionary.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="directoryPath">Path to the directory containing JSON translation files.</param>
    /// <param name="searchPattern">File search pattern (default: "*.json").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusJsonErrorLocalizer(
        this IServiceCollection services,
        string directoryPath,
        string searchPattern = "*.json")
    {
        services.AddSingleton<IKyrolusErrorLocalizer>(_ => new KyrolusJsonErrorLocalizer(directoryPath, searchPattern));
        return services;
    }
}
