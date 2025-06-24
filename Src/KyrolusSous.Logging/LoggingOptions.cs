global using KyrolusSous.Logging.Theming;
using System.Reflection;
using Serilog.Events;

namespace KyrolusSous.Logging;

public class LoggingOptions
{
    // --- Global Logging Settings ---
    public string ApplicationName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "DefaultApp";
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;
    public string DefaultOutputTemplate { get; set; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}";

    // --- Minimum Level Overrides (Per Namespace/Source) ---
    public Dictionary<string, LogEventLevel> MinimumLevelOverrides { get; set; } = new Dictionary<string, LogEventLevel>();

    // --- Console Logging Settings ---
    public bool EnableConsoleLogging { get; set; } = true;
    public LogEventLevel ConsoleMinimumLevel { get; set; } = LogEventLevel.Information;
    public ConsoleTheme? ConsoleTheme { get; set; } = KyrolusSous.Logging.Theming.CustomAnsiConsoleTheme.VisualStudioMacLight;
    public string ConsoleOutputTemplate { get; set; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
    public bool ConsoleApplyDefaultOutputTemplate { get; set; } = true;

    // --- File Logging Settings ---
    public bool EnableFileLogging { get; set; } = true;
    public LogEventLevel FileMinimumLevel { get; set; } = LogEventLevel.Information;
    public string LogFilePath { get; set; } = "Logs/log-.txt";
    public RollingInterval FileRollingInterval { get; set; } = RollingInterval.Day;
    public long? FileSizeLimitBytes { get; set; } = null;
    public int? FileRetainedFileCountLimit { get; set; } = 31;
    public bool FileBuffered { get; set; } = true;
    public bool FileShared { get; set; } = false;
    public bool UseCustomFileFormatter { get; set; } = true;
    public string FileOutputTemplate { get; set; } = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}";
    public bool FileApplyDefaultOutputTemplate { get; set; } = true;

    // --- Unified Enrichers Configuration ---
    // List of enrichers to enable, either by common type or custom Type
    public List<EnricherConfiguration> Enrichers { get; set; } = new List<EnricherConfiguration>();

    // --- ALL Sinks Configuration (Unified List) ---
    public List<SinkConfiguration> Sinks { get; set; } = new List<SinkConfiguration>();

    // --- Filters ---
    public List<string> ExcludeByMessageSubstring { get; set; } = new List<string>();
    public List<string> ExcludeBySourceContextPrefix { get; set; } = new List<string>();
    public List<Type> CustomFilterTypes { get; set; } = new List<Type>();

    // Nested class to configure individual Sinks
    public class SinkConfiguration
    {
        public Type SinkType { get; set; } = default!; // The Type of the Serilog.Sinks.xyz Sink
        public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information; // Minimum level for this specific sink
        public Dictionary<string, object?> Parameters { get; set; } = new Dictionary<string, object?>(); // Configuration parameters for the sink
    }

    // Nested class to configure individual Enrichers
    public class EnricherConfiguration
    {
        public CommonEnricherType? CommonType { get; set; } // For commonly used enrichers
        public Type? CustomType { get; set; } // For custom ILogEventEnricher implementations
        public Dictionary<string, object?> Parameters { get; set; } = new Dictionary<string, object?>(); // Parameters for the enricher (e.g., property name)
    }

    // Enum for commonly used built-in enrichers
    public enum CommonEnricherType
    {
        None,
        FromLogContext, // Enrich.FromLogContext()
        MachineName,    // Enrich.WithMachineName()
        ProcessId,      // Enrich.WithProcessId()
        ProcessName,    // Enrich.WithProcessName()
        ThreadId,       // Enrich.WithThreadId()
        ThreadName,     // Enrich.WithThreadName()
        EnvironmentUserName, // Enrich.WithEnvironmentUserName()
        EnvironmentName, // Enrich.WithEnvironmentName()
        Demystify,      // Enrich.WithDemystifiedStackTraces() - requires Serilog.Exceptions and Serilog.Exceptions.Demystifier
        HttpRequestId,  // For ASP.NET Core request IDs - requires Serilog.AspNetCore
        HttpRequestNumber // For ASP.NET Core request sequence number - requires Serilog.AspNetCore
    }
}