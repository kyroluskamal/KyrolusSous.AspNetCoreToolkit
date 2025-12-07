using KyrolusSous.Logging.Theming;
using Serilog.Formatting;

namespace KyrolusSous.Logging;

/// <summary>
/// Defines the configuration options for the Kyrolus.Logging library.
/// </summary>
public class LoggingOptions
{
    // --- Global Logging Settings ---
    public LoggingOptions()
    {
        // Initialize the Enrichers list with its defaults.
        Enrichers =
        [
            new() { CommonType = CommonEnricherType.FromLogContext },
            new() { CommonType = CommonEnricherType.MachineName },
            new() { CommonType = CommonEnricherType.ThreadId },
            new() { CommonType = CommonEnricherType.ProcessId },
            new() { CommonType = CommonEnricherType.EnvironmentName }
        ];

        Sinks =
        [
            new()
            {
                CommonType = CommonSinkType.Console,
                SinkOptions = new ConsoleSinkOptions
                {
                    OutputTemplate = this.DefaultOutputTemplate
                }
            },
            new()
            {
                CommonType = CommonSinkType.File,
                MinimumLevel = LogEventLevel.Information,
                SinkOptions = new FileSinkOptions
                {
                    Path = "Logs/log-.txt",
                    RollingInterval = RollingInterval.Day,
                    RetainedFileCountLimit = 31,
                    OutputTemplate = this.DefaultOutputTemplate
                }
            }
        ];
    }
    /// <summary>
    /// The name of the application, used for enriching log events.
    /// Defaults to the entry assembly name.
    /// </summary>
    public string ApplicationName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "DefaultApp";

    /// <summary>
    /// The global minimum level for logging. Events below this level will be discarded.
    /// Default is Information.
    /// </summary>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// The default output template for log messages. Can be overridden per sink.
    /// </summary>
    public string DefaultOutputTemplate { get; set; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}";

    // --- Minimum Level Overrides (Per Namespace/Source) ---

    /// <summary>
    /// Overrides the minimum log level for specific source contexts (e.g., "Microsoft.AspNetCore").
    /// </summary>
    public Dictionary<string, LogEventLevel> MinimumLevelOverrides { get; set; } = [];

    // --- Sinks Configuration ---

    /// <summary>
    /// A list of sink configurations that define where log events are sent.
    /// All sinks, including Console and File, should be configured here.
    /// </summary>
    public List<SinkConfiguration> Sinks { get; set; } = [];

    // --- Enrichers Configuration ---

    /// <summary>
    /// A list of enricher configurations to add extra information to log events.
    /// </summary>
    public List<EnricherConfiguration> Enrichers { get; set; } = [];

    // --- Filters ---

    /// <summary>
    /// Excludes log events that contain any of the specified substrings in their rendered message.
    /// </summary>
    public List<string> ExcludeByMessageSubstring { get; set; } = [];

    /// <summary>
    /// Excludes log events where the source context starts with any of the specified prefixes.
    /// </summary>
    public List<string> ExcludeBySourceContextPrefix { get; set; } = [];
    // --- Nested Configuration Classes ---

    /// <summary>
    /// Configures a single log sink (destination). This is the FINAL version.
    /// </summary>
    public class SinkConfiguration
    {
        /// <summary>
        /// (Easy Mode) The type of a common, well-known sink.
        /// </summary>
        public CommonSinkType CommonType { get; set; }

        /// <summary>
        /// The minimum log level required for events to be written to this sink.
        /// </summary>
        public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

        /// <summary>
        /// Provides the configuration options for the sink.
        /// This can be a strongly-typed options object (e.g., new FileSinkOptions())
        /// OR a Dictionary<string, object?> for advanced sinks.
        /// </summary>
        public object? SinkOptions { get; set; }

        // --- The properties below are for the MOST advanced/manual path ---
        public Type? CustomType { get; set; }
        public string? SinkMethodName { get; set; }
        public string? SinkPackageName { get; set; }
    }

    // --- Strongly-Typed Options Classes for better Developer Experience ---

    public class FileSinkOptions
    {
        public string Path { get; set; } = "Logs/log.txt";
        public RollingInterval RollingInterval { get; set; } = RollingInterval.Day;
        public int? RetainedFileCountLimit { get; set; } = 31;
        public required string OutputTemplate { get; set; }
    }

    public class SeqSinkOptions
    {
        public string ServerUrl { get; set; } = "http://localhost:5341";
        public string? ApiKey { get; set; }
    }
    /// <summary>
    /// Provides strongly-typed options for the Console sink.
    /// </summary>
    public class ConsoleSinkOptions
    {
        public string? OutputTemplate { get; set; }
        public ConsoleTheme? Theme { get; set; } = CustomAnsiConsoleTheme.VisualStudioMacLight;
        public ITextFormatter? Formatter { get; set; } = new CustomTextFormatter();

    }
    /// <summary>
    /// Configures a single log event enricher.
    /// </summary>
    public class EnricherConfiguration
    {
        /// <summary>
        /// (Easy Mode) The type of a common, built-in enricher.
        /// This has the highest precedence.
        /// </summary>
        public CommonEnricherType? CommonType { get; set; }

        /// <summary>
        /// (Custom Class Mode) The type of a class that implements ILogEventEnricher.
        /// Used if CommonType is null.
        /// </summary>
        public Type? CustomType { get; set; }

        /// <summary>
        /// (Advanced Extension Method Mode) The name of the extension method for the Enricher.
        /// Used only when CommonType and CustomType are null.
        /// </summary>
        public string? MethodName { get; set; }

        /// <summary>
        /// (Advanced Extension Method Mode) The name of the NuGet package that contains the Enricher.
        /// Used only when CommonType and CustomType are null.
        /// </summary>
        public string? PackageName { get; set; }

        /// <summary>
        /// A dictionary of parameters for the enricher (rarely used).
        /// </summary>
        public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An enumeration of common, well-known sinks for easy configuration.
    /// The documentation for each member lists the most common parameters available.
    /// </summary>
    public enum CommonSinkType
    {
        /// <summary>
        /// Represents no sink. This is the default and will be ignored.
        /// </summary>
        None,

        /// <summary>
        /// Writes log events to the standard console output. Very useful for development.
        /// ---
        /// Common Parameters:
        /// - outputTemplate (string): The message template to format log messages.
        /// - theme (ConsoleTheme): The theme for styling the output (e.g., CustomAnsiConsoleTheme.VisualStudioMacLight, AnsiConsoleTheme.Code).
        /// </summary>
        Console,

        /// <summary>
        /// Writes log events to a rolling file. A new file is created based on the rolling interval.
        /// ---
        /// Common Parameters:
        /// - path (string): The path to the log file. Can include {Date} for date-based rolling. Example: "logs/log-{Date}.txt".
        /// - rollingInterval (RollingInterval): The interval for creating new files (e.g., Day, Hour, Minute).
        /// - retainedFileCountLimit (int?): The maximum number of log files to keep. Use null for no limit. Default is 31.
        /// - fileSizeLimitBytes (long?): The maximum size of a single log file in bytes. Use null for no limit.
        /// - formatter (ITextFormatter): A custom formatter instance (e.g., new JsonFormatter()). If provided, this overrides outputTemplate.
        /// - outputTemplate (string): The message template if a custom formatter is not used.
        /// </summary>
        File,

        /// <summary>
        /// Sends log events over HTTP to a Seq server for powerful, structured log searching and analysis.
        /// ---
        /// Common Parameters:
        /// - serverUrl (string): The URL of your Seq server. Example: "http://localhost:5341".
        /// - apiKey (string): [Optional] A Seq API key if required for authentication.
        /// </summary>
        Seq,

        /// <summary>
        /// Writes log events to a table in a Microsoft SQL Server database.
        /// ---
        /// Common Parameters:
        /// - connectionString (string): The connection string to the SQL Server database.
        /// - sinkOptions (MSSqlServerSinkOptions object): An object for advanced configuration like table name, schema, and column options.
        /// </summary>
        MSSqlServer,

        /// <summary>
        /// Sends log events to an Elasticsearch cluster for powerful indexing and searching capabilities.
        /// ---
        /// Common Parameters:
        /// - nodeUris (string): A comma-separated list of Elasticsearch node URIs. Example: "http://localhost:9200".
        /// - indexFormat (string): The format for the index name, usually including a date. Example: "my-app-logs-{0:yyyy.MM.dd}".
        /// - autoRegisterTemplate (bool): [Optional] Set to true to automatically register an index template. Default is false.
        /// </summary>
        Elasticsearch,

        /// <summary>
        /// Writes log events to a table in a PostgreSQL database.
        /// ---
        /// Common Parameters:
        /// - connectionString (string): The connection string to the PostgreSQL database.
        /// - tableName (string): The name of the table where logs will be stored.
        /// - needAutoCreateTable (bool): [Optional] Set to true to automatically create the log table if it doesn't exist. Default is false.
        /// </summary>
        PostgreSQL,

        /// <summary>
        /// Writes log events to a local SQLite database file. Useful for simple, self-contained applications.
        /// ---
        /// Common Parameters:
        /// - sqliteDbPath (string): The path to the SQLite database file.
        /// - tableName (string): The name of the table to store log events. Default is "Logs".
        /// - storeTimestampInUtc (bool): [Optional] Set to true to store timestamps in UTC. Default is false.
        /// </summary>
        SQLite
    }

    /// <summary>
    /// An enumeration of common, built-in enrichers.
    /// </summary>
    public enum CommonEnricherType
    {
        None,
        FromLogContext,
        MachineName,
        ProcessId,
        ProcessName,
        ThreadId,
        ThreadName,
        EnvironmentUserName,
        EnvironmentName,
        HttpRequestId,
    }
}