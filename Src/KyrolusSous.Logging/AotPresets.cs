using KyrolusSous.Logging.Theming;

namespace KyrolusSous.Logging;

/// <summary>
/// Provides helper methods to configure AOT-friendly logging without reflection.
/// </summary>
public static class AotPresets
{
    /// <summary>
    /// Disables reflection discovery and registers console and file sinks using the current options/formatter settings.
    /// </summary>
    public static void UseAotDefaults(this LoggingOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        options.UseReflectionDiscovery = false;

        if (options.AotEnricherRegistrations.Count == 0)
        {
            options.AotEnricherRegistrations.Add(enrich => enrich.FromLogContext());
        }

        if (options.AotSinkRegistrations.Count == 0)
        {
            options.AotSinkRegistrations.Add(cfg =>
                cfg.WriteTo.Console(formatter: new CustomTextFormatter(options.FormatterOptionsBySink.GetValueOrDefault("Console", options.DefaultFormatterOptions))));

            options.AotSinkRegistrations.Add(cfg =>
                cfg.WriteTo.File(
                    path: Path.Combine(environment.ContentRootPath, "Logs", "log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: options.DefaultOutputTemplate));
        }
    }
}
