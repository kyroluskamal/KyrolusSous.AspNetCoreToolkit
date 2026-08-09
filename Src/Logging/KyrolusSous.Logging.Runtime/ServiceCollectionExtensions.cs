namespace KyrolusSous.Logging.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusLoggingRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IKyrolusLoggerFactory, KyrolusLoggerFactory>();
        services.AddSingleton(typeof(IKyrolusLogger<>), typeof(KyrolusLogger<>));
        services.AddSingleton<IKyrolusLogger>(sp =>
        {
            var factory = sp.GetRequiredService<ILoggerFactory>();
            return new KyrolusLogger(factory.CreateLogger("Kyrolus"));
        });

        return services;
    }
}
