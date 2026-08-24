using KyrolusSous.Logging.Abstractions.LevelSwitch;
using KyrolusSous.Logging.Core.Exceptions;
using KyrolusSous.Logging.Core.Filters;
using KyrolusSous.Logging.Core.LevelSwitch;
using KyrolusSous.Logging.Core.Masking;
using KyrolusSous.Logging.Core.Middleware;
using KyrolusSous.Logging.Core.Redaction;

namespace KyrolusSous.Logging.Core;

/// <summary>
/// Options for configuring the core logging features.
/// </summary>
public sealed class KyrolusLoggingCoreOptions
{
    /// <summary>
    /// Gets or sets custom sensitive property names to be automatically masked.
    /// </summary>
    public List<string> CustomSensitiveKeywords { get; set; } = [];

    /// <summary>
    /// Gets or sets the initial minimum log level for the dynamic level switch.
    /// </summary>
    public LogLevel InitialMinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the maximum duplicate message count before throttling.
    /// </summary>
    public int MaxDuplicateMessagesPerWindow { get; set; } = 5;

    /// <summary>
    /// Gets or sets the rate limiter window duration.
    /// </summary>
    public TimeSpan RateLimitingWindow { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Dependency injection extension methods for <c>KyrolusSous.Logging.Core</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core logging services, sensitive data masker, rate limiter, level switch, and factory into DI.
    /// </summary>
    public static IServiceCollection AddKyrolusLoggingCore(this IServiceCollection services, Action<KyrolusLoggingCoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new KyrolusLoggingCoreOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IKyrolusStringRedactor, KyrolusStringRedactor>();
        services.TryAddSingleton<IKyrolusLogLevelSwitch>(_ => new KyrolusLogLevelSwitch(options.InitialMinimumLevel));
        services.TryAddSingleton<KyrolusExceptionSanitizer>();
        services.TryAddSingleton(_ => new KyrolusLogRateLimiter(options.MaxDuplicateMessagesPerWindow, options.RateLimitingWindow));

        services.TryAddSingleton<IKyrolusDataMasker>(sp =>
        {
            var redactor = sp.GetRequiredService<IKyrolusStringRedactor>();
            return new KyrolusSensitiveDataMasker(options.CustomSensitiveKeywords, redactor);
        });

        services.TryAddSingleton<IKyrolusLoggerFactory, KyrolusLoggerFactory>();
        services.TryAddSingleton(typeof(IKyrolusLogger<>), typeof(KyrolusLogger<>));
        services.TryAddSingleton<IKyrolusLogger>(sp =>
        {
            var factory = sp.GetRequiredService<ILoggerFactory>();
            var masker = sp.GetRequiredService<IKyrolusDataMasker>();
            return new KyrolusLogger(factory.CreateLogger("Kyrolus"), masker);
        });

        return services;
    }

    /// <summary>
    /// Registers the enterprise HTTP logging middleware options.
    /// </summary>
    public static IServiceCollection AddKyrolusHttpLogging(this IServiceCollection services, Action<KyrolusHttpLoggingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKyrolusLoggingCore();
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<KyrolusHttpLoggingOptions>();
        }

        return services;
    }

    /// <summary>
    /// Adds the enterprise HTTP Request/Response logging middleware into the ASP.NET Core pipeline.
    /// </summary>
    public static IApplicationBuilder UseKyrolusHttpLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<KyrolusHttpLoggingMiddleware>();
    }
}
