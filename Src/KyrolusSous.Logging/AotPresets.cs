using KyrolusSous.Logging.Theming;
using Serilog.Configuration;

namespace KyrolusSous.Logging;

/// <summary>
/// Provides helper methods to configure AOT-friendly logging without reflection.
/// </summary>
public static class AotPresets
{
    private static readonly Action<LoggerEnrichmentConfiguration> DefaultAotFromLogContext = enrich => enrich.FromLogContext();

    /// <summary>
    /// Disables reflection discovery and registers console and file sinks using the current options/formatter settings.
    /// </summary>
    public static void UseAotDefaults(this LoggingOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        if (options.AotDefaultsApplied)
        {
            return;
        }

        options.UseReflectionDiscovery = false;

        if (!options.AotEnricherRegistrations.Contains(DefaultAotFromLogContext))
        {
            options.AotEnricherRegistrations.Insert(0, DefaultAotFromLogContext);
        }

        options.AotSinkRegistrations.Insert(0, cfg =>
            cfg.WriteTo.Console(formatter: new CustomTextFormatter(options.FormatterOptionsBySink.GetValueOrDefault("Console", options.DefaultFormatterOptions))));

        options.AotSinkRegistrations.Add(cfg =>
            cfg.WriteTo.File(
                path: Path.Combine(environment.ContentRootPath, "Logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: options.DefaultOutputTemplate));

        options.AotDefaultsApplied = true;
    }
}
