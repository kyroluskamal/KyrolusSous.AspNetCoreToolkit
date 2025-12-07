using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


namespace KyrolusSous.Logging;

/// <summary>
/// Provides extension methods for easily configuring Serilog in a .NET application.
/// </summary>
public static class SerilogServiceExtension
{
    /// <summary>
    /// Registers and configures LoggingOptions in the dependency injection container.
    /// This method binds configuration from appsettings.json ("Logging" section) and allows for further customization via a delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="configureOptions">An optional action to further configure the logging options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusLogging(this IServiceCollection services, IConfiguration configuration, Action<LoggingOptions>? configureOptions = null)
    {
        // Register the options pattern
        var optionsBuilder = services.AddOptions<LoggingOptions>();
        // Bind the "Logging" section from appsettings.json to our options class
        optionsBuilder.Configure(options => configuration.GetSection("Logging").Bind(options));

        // If the user provided a configuration delegate, apply it
        if (configureOptions != null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        return services;
    }

    /// <summary>
    /// Configures Serilog as the logging provider for the host, using the LoggingOptions registered in the DI container.
    /// Ensure AddKyrolusLogging() is called on the IServiceCollection first.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder UseKyrolusLogging(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((hostingContext, services, loggerConfiguration) =>
        {
            // Resolve the configured options from the DI container
            var options = services.GetRequiredService<IOptions<LoggingOptions>>().Value;

            // Delegate the complex configuration logic to our internal builder class
            LoggerConfigurationBuilder.Build(loggerConfiguration, options, hostingContext.HostingEnvironment);
        });

        return hostBuilder;
    }
}
