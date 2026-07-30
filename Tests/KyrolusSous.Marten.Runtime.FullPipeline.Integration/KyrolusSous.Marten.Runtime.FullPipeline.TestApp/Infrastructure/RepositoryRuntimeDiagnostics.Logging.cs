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
using System.Reflection;

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

            var builderType = typeof(KyrolusSous.Logging.Serilog.LoggerConfigurationBuilder);
            var convertOptionsToDictionaryMethod = builderType.GetMethod("ConvertOptionsToDictionary", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ConvertOptionsToDictionary method was not found.");
            var prepareSinkParametersMethod = builderType.GetMethod("PrepareSinkParameters", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("PrepareSinkParameters method was not found.");
            var getSinkDetailsMethod = builderType.GetMethod("GetSinkDetails", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("GetSinkDetails method was not found.");
            var getSinkKeyMethod = builderType.GetMethod("GetSinkKey", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("GetSinkKey method was not found.");
            var tryConvertParameterMethod = builderType.GetMethod("TryConvertParameter", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TryConvertParameter method was not found.");
            var tryGetArgumentsForMethodMethod = builderType.GetMethod("TryGetArgumentsForMethod", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TryGetArgumentsForMethod method was not found.");
            var findBestMethodOverloadMethod = builderType.GetMethod("FindBestMethodOverload", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("FindBestMethodOverload method was not found.");
            var toCamelCaseMethod = builderType.GetMethod("ToCamelCase", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ToCamelCase method was not found.");

            var nullOptionsDictionary = (Dictionary<string, object?>)convertOptionsToDictionaryMethod.Invoke(null, [null])!;
            var rawOptionsDictionary = (Dictionary<string, object?>)convertOptionsToDictionaryMethod.Invoke(
                null,
                [new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["Path"] = "logs/runtime.txt" }])!;
            var typedOptionsDictionary = (Dictionary<string, object?>)convertOptionsToDictionaryMethod.Invoke(
                null,
                [new LoggingOptions.FileSinkOptions
                {
                    Path = "Logs/probe-.txt",
                    OutputTemplate = "[{Level}] {Message:lj}"
                }])!;
            Require(
                nullOptionsDictionary.Count == 0 &&
                rawOptionsDictionary.TryGetValue("Path", out var rawPathValue) &&
                (string?)rawPathValue == "logs/runtime.txt" &&
                typedOptionsDictionary.TryGetValue("path", out var typedPathValue) &&
                (string?)typedPathValue == "Logs/probe-.txt" &&
                typedOptionsDictionary.ContainsKey("outputTemplate"),
                "Logger configuration builder should normalize null, dictionary, and typed sink options into a consistent dictionary shape.",
                ref checks);

            var relativeFileParameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "Logs/relative-file-.txt"
            };
            prepareSinkParametersMethod.Invoke(null, [relativeFileParameters, LoggingOptions.CommonSinkType.File, aotEnvironment, reflectionOptions]);
            var consoleParameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["formatter"] = new CustomTextFormatter(reflectionOptions.DefaultFormatterOptions)
            };
            prepareSinkParametersMethod.Invoke(null, [consoleParameters, LoggingOptions.CommonSinkType.Console, aotEnvironment, reflectionOptions]);
            Require(
                relativeFileParameters.TryGetValue("outputTemplate", out var defaultTemplateValue) &&
                (string?)defaultTemplateValue == reflectionOptions.DefaultOutputTemplate &&
                relativeFileParameters.TryGetValue("path", out var relativePathValue) &&
                string.Equals((string?)relativePathValue, Path.Combine(tempRoot, "Logs/relative-file-.txt"), StringComparison.OrdinalIgnoreCase) &&
                !consoleParameters.ContainsKey("outputTemplate"),
                "Logger configuration builder should inject default templates only when needed and normalize relative file paths.",
                ref checks);

            var commonSinkDetails = ((string? MethodName, string? PackageName))getSinkDetailsMethod.Invoke(
                null,
                [new LoggingOptions.SinkConfiguration { CommonType = LoggingOptions.CommonSinkType.Console }])!;
            var manualSinkDetails = ((string? MethodName, string? PackageName))getSinkDetailsMethod.Invoke(
                null,
                [new LoggingOptions.SinkConfiguration
                {
                    CommonType = LoggingOptions.CommonSinkType.None,
                    SinkMethodName = "CustomSink",
                    SinkPackageName = "Custom.Package"
                }])!;
            var missingSinkDetails = ((string? MethodName, string? PackageName))getSinkDetailsMethod.Invoke(
                null,
                [new LoggingOptions.SinkConfiguration()])!;
            Require(
                commonSinkDetails == ("Console", "Serilog.Sinks.Console") &&
                manualSinkDetails == ("CustomSink", "Custom.Package") &&
                missingSinkDetails == (null, null),
                "Logger configuration builder should resolve common, manual, and missing sink metadata correctly.",
                ref checks);

            var sinkKeys = new[]
            {
                (string)getSinkKeyMethod.Invoke(null, [new LoggingOptions.SinkConfiguration { CommonType = LoggingOptions.CommonSinkType.File }])!,
                (string)getSinkKeyMethod.Invoke(null, [new LoggingOptions.SinkConfiguration { SinkMethodName = "Seq" }])!,
                (string)getSinkKeyMethod.Invoke(null, [new LoggingOptions.SinkConfiguration { CustomType = typeof(LoggingProbeSink) }])!,
                (string)getSinkKeyMethod.Invoke(null, [new LoggingOptions.SinkConfiguration()])!
            };
            Require(
                sinkKeys.SequenceEqual(["File", "Seq", nameof(LoggingProbeSink), "default"]),
                "Logger configuration builder should derive sink keys from common, manual, custom, and fallback configurations.",
                ref checks);

            var enumConversionArgs = new object?[] { (int)RollingInterval.Day, typeof(RollingInterval), null };
            var enumConversionSucceeded = (bool)tryConvertParameterMethod.Invoke(null, enumConversionArgs)!;
            var directConversionArgs = new object?[] { "text", typeof(string), null };
            var directConversionSucceeded = (bool)tryConvertParameterMethod.Invoke(null, directConversionArgs)!;
            var invalidConversionArgs = new object?[] { "bad-number", typeof(int), null };
            var invalidConversionSucceeded = (bool)tryConvertParameterMethod.Invoke(null, invalidConversionArgs)!;
            Require(
                enumConversionSucceeded &&
                enumConversionArgs[2] is RollingInterval.Day &&
                directConversionSucceeded &&
                (string?)directConversionArgs[2] == "text" &&
                !invalidConversionSucceeded,
                "Logger configuration builder should convert enum and direct parameter values while rejecting invalid conversions.",
                ref checks);

            var requiredAndOptionalMethod = typeof(RuntimeLoggingMethodHolder).GetMethod(nameof(RuntimeLoggingMethodHolder.RequiredAndOptional))
                ?? throw new InvalidOperationException("RequiredAndOptional method was not found.");
            var methodArgs = new object?[]
            {
                requiredAndOptionalMethod,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["required"] = 7
                },
                null,
                0
            };
            var matchedRequiredAndOptional = (bool)tryGetArgumentsForMethodMethod.Invoke(null, methodArgs)!;
            var missingRequiredArgs = new object?[]
            {
                requiredAndOptionalMethod,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                null,
                0
            };
            var matchedMissingRequired = (bool)tryGetArgumentsForMethodMethod.Invoke(null, missingRequiredArgs)!;
            Require(
                matchedRequiredAndOptional &&
                methodArgs[2] is List<object?> resolvedArguments &&
                resolvedArguments.Count == 2 &&
                resolvedArguments[0] is int requiredValue && requiredValue == 7 &&
                (string?)resolvedArguments[1] == "fallback" &&
                (int)methodArgs[3]! == 1 &&
                !matchedMissingRequired,
                "Logger configuration builder should build optional argument lists only when required parameters are present.",
                ref checks);

            var overloads = typeof(RuntimeLoggingMethodHolder)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(static method => method.Name == nameof(RuntimeLoggingMethodHolder.Overload))
                .ToList();
            var overloadResolution = ((MethodInfo? BestMethod, List<object?>? SortedArgs))findBestMethodOverloadMethod.Invoke(
                null,
                [
                    overloads,
                    new object(),
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["required"] = 9,
                        ["extra"] = "picked"
                    }
                ])!;
            Require(
                overloadResolution.BestMethod?.GetParameters().Length == 3 &&
                overloadResolution.SortedArgs is { Count: 3 } overloadArgs &&
                (int)overloadArgs[1]! == 9 &&
                (string?)overloadArgs[2] == "picked",
                "Logger configuration builder should choose the overload that satisfies the most provided parameters.",
                ref checks);

            Require(
                (string)toCamelCaseMethod.Invoke(null, ["OutputTemplate"])! == "outputTemplate" &&
                (string)toCamelCaseMethod.Invoke(null, ["alreadyCamel"])! == "alreadyCamel" &&
                (string)toCamelCaseMethod.Invoke(null, [""])! == string.Empty,
                "Logger configuration builder should preserve empty and camelCase names while lowering PascalCase names.",
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

internal static class RuntimeLoggingMethodHolder
{
    public static void RequiredAndOptional(object configuration, int required, string optional = "fallback")
    {
        _ = configuration;
        _ = required;
        _ = optional;
    }

    public static void Overload(object configuration, int required)
    {
        _ = configuration;
        _ = required;
    }

    public static void Overload(object configuration, int required, string extra = "extra")
    {
        _ = configuration;
        _ = required;
        _ = extra;
    }
}
