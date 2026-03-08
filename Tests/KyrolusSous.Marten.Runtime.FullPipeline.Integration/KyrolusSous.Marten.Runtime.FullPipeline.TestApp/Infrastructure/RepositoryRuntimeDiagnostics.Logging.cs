using System.Collections.Concurrent;
using KyrolusSous.Logging.Serilog;
using KyrolusSous.Logging.Serilog.Theming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.SystemConsole.Themes;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunLoggingRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var tempRoot = Path.Combine(Path.GetTempPath(), "kyrolus-logging-runtime", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:ApplicationName"] = "ConfiguredApp",
                    ["Logging:MinimumLevel"] = nameof(LogEventLevel.Warning),
                    ["Logging:MinimumLevelOverrides:Microsoft"] = nameof(LogEventLevel.Error)
                })
                .Build();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddKyrolusLogging(configuration, options =>
            {
                options.MinimumLevelOverrides["System"] = LogEventLevel.Fatal;
                options.ThrowIfPackageMissing = false;
            });

            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var options = serviceProvider.GetRequiredService<IOptions<LoggingOptions>>().Value;
                Require(
                    options.ApplicationName == "ConfiguredApp" &&
                    options.MinimumLevel == LogEventLevel.Warning &&
                    options.MinimumLevelOverrides["Microsoft"] == LogEventLevel.Error &&
                    options.MinimumLevelOverrides["System"] == LogEventLevel.Fatal,
                    "AddKyrolusLogging should bind configuration and apply overrides.",
                    ref checks);
            }

            var aotEnvironment = new RuntimeHostEnvironment(Environments.Development)
            {
                ContentRootPath = tempRoot
            };
            var aotOptions = new LoggingOptions();
            aotOptions.UseAotDefaults(aotEnvironment);
            aotOptions.UseAotDefaults(aotEnvironment);
            Require(
                !aotOptions.UseReflectionDiscovery &&
                aotOptions.AotEnricherRegistrations.Count == 1 &&
                aotOptions.AotSinkRegistrations.Count == 2,
                "AOT defaults should apply only once and register the expected delegates.",
                ref checks);

            var aotLoggerConfiguration = new LoggerConfiguration();
            KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(aotLoggerConfiguration, aotOptions, aotEnvironment);
            using (var aotLogger = aotLoggerConfiguration.CreateLogger())
            {
                aotLogger.Information("AOT logger writes to file");
            }

            Require(
                Directory.Exists(Path.Combine(tempRoot, "Logs")) &&
                Directory.GetFiles(Path.Combine(tempRoot, "Logs"), "log-*.txt", SearchOption.TopDirectoryOnly).Length > 0,
                "AOT defaults should normalize the file sink path under the host content root.",
                ref checks);

            LoggingProbeSink.Reset();
            using (var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection())
                .ConfigureServices((context, services) =>
                {
                    services.AddKyrolusLogging(context.Configuration, options =>
                    {
                        options.UseReflectionDiscovery = false;
                        options.AotEnricherRegistrations.Clear();
                        options.AotSinkRegistrations.Clear();
                        options.AotEnricherRegistrations.Add(enrich => enrich.WithProperty("AotPath", "enabled"));
                        options.AotSinkRegistrations.Add(logger => logger.WriteTo.Sink(new LoggingProbeSink(), LogEventLevel.Information));
                    });
                })
                .UseKyrolusLogging()
                .Build())
            {
                var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Diagnostics.Host");
                logger.LogInformation("host logging path");
                await host.StopAsync(cancellationToken).ConfigureAwait(false);
            }

            Require(
                LoggingProbeSink.Snapshot().Any(logEvent => logEvent.Properties.ContainsKey("AotPath")),
                "UseKyrolusLogging should build a Serilog pipeline from DI options.",
                ref checks);

            LoggingProbeSink.Reset();
            var reflectionOptions = new LoggingOptions
            {
                ApplicationName = "ReflectionDiagnostics",
                ThrowIfPackageMissing = true,
                DefaultOutputTemplate = "[{Level}] {Message:lj}",
                DefaultFormatterOptions = new TextFormatterOptions
                {
                    UseColors = false,
                    ShowProperties = true,
                    ShowSourceContext = true,
                    ShowException = true,
                    ExceptionDetail = TextFormatterOptions.ExceptionDetailLevel.Full
                },
                Enrichers =
                [
                    new LoggingOptions.EnricherConfiguration { CustomType = typeof(LoggingProbeEnricher) }
                ],
                Sinks =
                [
                    new LoggingOptions.SinkConfiguration
                    {
                        CustomType = typeof(LoggingProbeSink),
                        MinimumLevel = LogEventLevel.Warning
                    },
                    new LoggingOptions.SinkConfiguration
                    {
                        CommonType = LoggingOptions.CommonSinkType.File,
                        MinimumLevel = LogEventLevel.Information,
                        SinkOptions = new LoggingOptions.FileSinkOptions
                        {
                            Path = "Logs/reflection-log-.txt",
                            RollingInterval = RollingInterval.Day,
                            RetainedFileCountLimit = 2,
                            OutputTemplate = "[{Level}] {Message:lj}"
                        }
                    }
                ]
            };
            reflectionOptions.ExcludeByMessageSubstring.Add("skip-me");
            reflectionOptions.ExcludeBySourceContextPrefix.Add("Filtered.Source");

            var reflectionConfiguration = new LoggerConfiguration();
            KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(reflectionConfiguration, reflectionOptions, aotEnvironment);
            using (var reflectionLogger = reflectionConfiguration.CreateLogger())
            {
                reflectionLogger.Warning("keep me");
                reflectionLogger.Warning("skip-me");
                reflectionLogger.ForContext(Constants.SourceContextPropertyName, "Filtered.Source.Component")
                    .Warning("filtered by source");
            }

            var reflectionEvents = LoggingProbeSink.Snapshot();
            Require(
                reflectionEvents.Length == 1 &&
                reflectionEvents[0].Properties.ContainsKey("ProbeEnricher") &&
                Directory.GetFiles(Path.Combine(tempRoot, "Logs"), "reflection-log-*.txt", SearchOption.TopDirectoryOnly).Length > 0,
                "Reflection logging should apply custom sinks, enrichers, filters, and file path normalization.",
                ref checks);

            var manualOptions = new LoggingOptions
            {
                ThrowIfPackageMissing = true,
                Enrichers = [],
                Sinks =
                [
                    new LoggingOptions.SinkConfiguration
                    {
                        SinkMethodName = "File",
                        SinkPackageName = "Serilog.Sinks.File",
                        MinimumLevel = LogEventLevel.Information,
                        SinkOptions = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["path"] = Path.Combine(tempRoot, "Logs", "manual-log-.txt"),
                            ["rollingInterval"] = (int)RollingInterval.Day,
                            ["outputTemplate"] = "[{Level}] {Message:lj}"
                        }
                    }
                ]
            };
            var manualConfiguration = new LoggerConfiguration();
            KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(manualConfiguration, manualOptions, aotEnvironment);
            using (var manualLogger = manualConfiguration.CreateLogger())
            {
                manualLogger.Information("manual sink path");
            }

            Require(
                Directory.GetFiles(Path.Combine(tempRoot, "Logs"), "manual-log-*.txt", SearchOption.TopDirectoryOnly).Length > 0,
                "Manual sink discovery should resolve overloads and convert enum parameters.",
                ref checks);

            var skipMissingOptions = new LoggingOptions
            {
                ThrowIfPackageMissing = false,
                Enrichers =
                [
                    new LoggingOptions.EnricherConfiguration
                    {
                        MethodName = "MissingEnricher",
                        PackageName = "Missing.Package"
                    }
                ],
                Sinks = []
            };
            KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(new LoggerConfiguration(), skipMissingOptions, aotEnvironment);
            checks++;

            ExpectThrows<InvalidOperationException>(() =>
                KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(
                    new LoggerConfiguration(),
                    new LoggingOptions
                    {
                        ThrowIfPackageMissing = true,
                        Enrichers =
                        [
                            new LoggingOptions.EnricherConfiguration
                            {
                                MethodName = "MissingEnricher",
                                PackageName = "Missing.Package"
                            }
                        ],
                        Sinks = []
                    },
                    aotEnvironment));
            checks++;

            ExpectThrows<InvalidOperationException>(() =>
                KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(
                    new LoggerConfiguration(),
                    new LoggingOptions
                    {
                        ThrowIfPackageMissing = true,
                        Enrichers =
                        [
                            new LoggingOptions.EnricherConfiguration { CustomType = typeof(string) }
                        ],
                        Sinks = []
                    },
                    aotEnvironment));
            checks++;

            ExpectThrows<InvalidOperationException>(() =>
                KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(
                    new LoggerConfiguration(),
                    new LoggingOptions
                    {
                        ThrowIfPackageMissing = true,
                        Enrichers =
                        [
                            new LoggingOptions.EnricherConfiguration { CustomType = typeof(LoggingBrokenEnricher) }
                        ],
                        Sinks = []
                    },
                    aotEnvironment));
            checks++;

            ExpectThrows<InvalidOperationException>(() =>
                KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(
                    new LoggerConfiguration(),
                    new LoggingOptions
                    {
                        ThrowIfPackageMissing = true,
                        Enrichers = [],
                        Sinks =
                        [
                            new LoggingOptions.SinkConfiguration { CustomType = typeof(string) }
                        ]
                    },
                    aotEnvironment));
            checks++;

            ExpectThrows<InvalidOperationException>(() =>
                KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder.Build(
                    new LoggerConfiguration(),
                    new LoggingOptions
                    {
                        ThrowIfPackageMissing = true,
                        Enrichers = [],
                        Sinks =
                        [
                            new LoggingOptions.SinkConfiguration { CustomType = typeof(LoggingBrokenSink) }
                        ]
                    },
                    aotEnvironment));
            checks++;

            ExpectThrows<ArgumentNullException>(() => _ = new CustomConsoleTheme(null!));
            checks++;
            ExpectThrows<ArgumentNullException>(() => _ = new CustomAnsiConsoleTheme(null!));
            checks++;

            var typedExceptionOutput = new StringWriter();
            new CustomTextFormatter(new TextFormatterOptions
            {
                UseColors = false,
                ShowProperties = true,
                ShowSourceContext = true,
                ShowException = true,
                ExceptionDetail = TextFormatterOptions.ExceptionDetailLevel.TypeAndMessage
            }).Format(CreateLogEvent(LogEventLevel.Error, "typed"), typedExceptionOutput);
            Require(
                typedExceptionOutput.ToString().Contains("Diagnostics.Source", StringComparison.Ordinal) &&
                typedExceptionOutput.ToString().Contains("Exception: InvalidOperationException: typed", StringComparison.Ordinal) &&
                typedExceptionOutput.ToString().Contains("Attempt: 1", StringComparison.Ordinal),
                "CustomTextFormatter should write headers, properties, and type/message exceptions.",
                ref checks);

            var fullExceptionOutput = new StringWriter();
            new CustomTextFormatter(new TextFormatterOptions
            {
                UseColors = false,
                ShowProperties = false,
                ShowSourceContext = false,
                ShowException = true,
                ExceptionDetail = TextFormatterOptions.ExceptionDetailLevel.None,
                ExceptionDetailByLevel = new Dictionary<LogEventLevel, TextFormatterOptions.ExceptionDetailLevel>
                {
                    [LogEventLevel.Error] = TextFormatterOptions.ExceptionDetailLevel.Full
                }
            }).Format(CreateLogEvent(LogEventLevel.Error, "full"), fullExceptionOutput);
            Require(
                fullExceptionOutput.ToString().Contains("Inner: Exception: inner", StringComparison.Ordinal) &&
                fullExceptionOutput.ToString().Contains("Stack:", StringComparison.Ordinal) &&
                !fullExceptionOutput.ToString().Contains("Attempt: 1", StringComparison.Ordinal),
                "Per-level exception detail overrides should support full exception output.",
                ref checks);

            var messageOnlyOutput = new StringWriter();
            new CustomTextFormatter(new TextFormatterOptions
            {
                UseColors = false,
                ShowProperties = false,
                ShowSourceContext = false,
                ShowException = true,
                ExceptionDetail = TextFormatterOptions.ExceptionDetailLevel.MessageOnly
            }).Format(CreateLogEvent(LogEventLevel.Warning, "message-only"), messageOnlyOutput);
            Require(
                messageOnlyOutput.ToString().Contains("Exception: message-only", StringComparison.Ordinal) &&
                !messageOnlyOutput.ToString().Contains("InvalidOperationException", StringComparison.Ordinal),
                "MessageOnly exception rendering should omit the exception type.",
                ref checks);

            var noExceptionOutput = new StringWriter();
            new CustomTextFormatter(new TextFormatterOptions
            {
                UseColors = false,
                ShowProperties = false,
                ShowSourceContext = false,
                ShowException = true,
                ExceptionDetail = TextFormatterOptions.ExceptionDetailLevel.None
            }).Format(CreateLogEvent(LogEventLevel.Information, "hidden"), noExceptionOutput);
            Require(
                !noExceptionOutput.ToString().Contains("Exception:", StringComparison.Ordinal),
                "None exception rendering should omit exception details entirely.",
                ref checks);

            var consoleThemeWriter = new StringWriter();
            var consoleThemeLength = CustomConsoleTheme.VisualStudioMacLight.Set(consoleThemeWriter, ConsoleThemeStyle.LevelInformation);
            CustomConsoleTheme.VisualStudioMacLight.Reset(consoleThemeWriter);
            Require(
                consoleThemeLength > 0 &&
                consoleThemeWriter.ToString().Contains("\u001b[0m", StringComparison.Ordinal) &&
                ReferenceEquals(CustomConsoleThemeColors.VisualStudioMacLight, CustomConsoleTheme.VisualStudioMacLight),
                "Custom console theme presets should apply ANSI styles and expose the shared preset instance.",
                ref checks);

            var ansiThemeWriter = new StringWriter();
            var ansiThemeLength = CustomAnsiConsoleTheme.VisualStudioMacLight.Set(ansiThemeWriter, ConsoleThemeStyle.LevelError);
            CustomAnsiConsoleTheme.VisualStudioMacLight.Reset(ansiThemeWriter);
            Require(
                ansiThemeLength > 0 &&
                ansiThemeWriter.ToString().Contains("\u001b[0m", StringComparison.Ordinal) &&
                ReferenceEquals(AnsiConsoleThemeColors.VisualStudioMacLight, CustomAnsiConsoleTheme.VisualStudioMacLight),
                "Custom ANSI theme presets should write configured styles and reset output.",
                ref checks);

            return new RepositoryRuntimeDiagnosticsResponse(
                Mode: "logging-runtime",
                LoggingChecks: checks);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Temporary log files are best-effort cleanup only.
            }
        }
    }

    private static LogEvent CreateLogEvent(LogEventLevel level, string exceptionMessage)
    {
        Exception exception;
        try
        {
            throw new InvalidOperationException(exceptionMessage, new Exception("inner"));
        }
        catch (Exception captured)
        {
            exception = captured;
        }

        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            exception,
            new MessageTemplateParser().Parse("Rendered {Value}"),
            [
                new LogEventProperty("Value", new ScalarValue("message")),
                new LogEventProperty("Attempt", new ScalarValue(1)),
                new LogEventProperty(Constants.SourceContextPropertyName, new ScalarValue("Diagnostics.Source"))
            ]);
    }
}

internal sealed class LoggingProbeSink : ILogEventSink
{
    private static ConcurrentQueue<LogEvent> events = new();

    public void Emit(LogEvent logEvent)
    {
        events.Enqueue(logEvent);
    }

    public static void Reset()
    {
        events = new ConcurrentQueue<LogEvent>();
    }

    public static LogEvent[] Snapshot()
    {
        return events.ToArray();
    }
}

internal sealed class LoggingProbeEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProbeEnricher", "enabled"));
    }
}

internal sealed class LoggingBrokenEnricher(string value) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Broken", value));
    }
}

internal sealed class LoggingBrokenSink(string value) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        _ = value;
    }
}
