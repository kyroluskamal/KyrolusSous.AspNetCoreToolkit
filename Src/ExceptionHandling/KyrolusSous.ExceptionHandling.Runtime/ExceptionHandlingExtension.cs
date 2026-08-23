namespace KyrolusSous.ExceptionHandling.Runtime;

public static class ExceptionHandlingExtension
{
    public static IServiceCollection AddKyrolusExceptionHandling(this IServiceCollection services, Action<KyrolusExceptionHandlingOptions>? configure = null)
    {
        if (configure is not null) services.Configure(configure);

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

    public static IApplicationBuilder UseKyrolusExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();

    public static IServiceCollection AddKyrolusExceptionHandlingLocalization<TResource>(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusErrorLocalizer>(sp =>
        {
            var localizer = sp.GetRequiredService<IStringLocalizer<TResource>>();
            return new KyrolusStringLocalizerErrorLocalizer(localizer);
        });

        return services;
    }

    public static IServiceCollection AddKyrolusExceptionHandlingLocalization(this IServiceCollection services, IReadOnlyDictionary<string, string> translations)
    {
        services.AddSingleton<IKyrolusErrorLocalizer>(_ => new KyrolusDictionaryErrorLocalizer(translations));
        return services;
    }
}
