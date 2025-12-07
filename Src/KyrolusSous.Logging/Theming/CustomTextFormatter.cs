using Serilog.Formatting;

namespace KyrolusSous.Logging.Theming;

/// <summary>
/// Options that control how the custom text formatter renders logs.
/// </summary>
public class TextFormatterOptions
{
    public bool UseColors { get; set; } = true;
    public bool ShowProperties { get; set; } = true;
    public bool ShowSourceContext { get; set; } = true;
    public bool ShowException { get; set; } = true;

    public enum ExceptionDetailLevel
    {
        None,
        MessageOnly,
        TypeAndMessage,
        Full
    }

    /// <summary>
    /// Default exception detail level when no per-level override is present.
    /// </summary>
    public ExceptionDetailLevel ExceptionDetail { get; set; } = ExceptionDetailLevel.TypeAndMessage;

    /// <summary>
    /// Optional per-level exception detail overrides (e.g., Full for Error/Fatal, MessageOnly for Info).
    /// </summary>
    public Dictionary<LogEventLevel, ExceptionDetailLevel> ExceptionDetailByLevel { get; set; } = new();

    public Dictionary<LogEventLevel, string> LevelColors { get; set; } = new()
    {
        [LogEventLevel.Verbose] = "\x1b[36m",
        [LogEventLevel.Debug] = "\x1b[36m",
        [LogEventLevel.Information] = "\x1b[32m",
        [LogEventLevel.Warning] = "\x1b[33m",
        [LogEventLevel.Error] = "\x1b[31m",
        [LogEventLevel.Fatal] = "\x1b[35m"
    };
}

/// <summary>
/// A colored text formatter that can be customized via <see cref="TextFormatterOptions"/>.
/// </summary>
public class CustomTextFormatter(TextFormatterOptions? options = null) : ITextFormatter
{
    private const string Reset = "\x1b[0m";
    private readonly TextFormatterOptions _options = options ?? new TextFormatterOptions();

    public void Format(LogEvent logEvent, TextWriter output)
    {
        WriteHeader(logEvent, output);

        if (_options.ShowProperties && logEvent.Properties.Count > 0)
        {
            WriteProperties(logEvent, output);
        }

        if (_options.ShowException && logEvent.Exception != null)
        {
            WriteException(logEvent, output);
        }
    }

    private void WriteHeader(LogEvent logEvent, TextWriter output)
    {
        var color = _options.UseColors && _options.LevelColors.TryGetValue(logEvent.Level, out var c) ? c : string.Empty;
        var level = FormatLevel(logEvent.Level);
        var source = _options.ShowSourceContext && logEvent.Properties.TryGetValue("SourceContext", out var src)
            ? $" (src={src})"
            : string.Empty;

        output.WriteLine($"{color}[{logEvent.Timestamp:HH:mm:ss.fff}] [{level}]{source} {logEvent.RenderMessage()}{(_options.UseColors ? Reset : string.Empty)}");
    }

    private static void WriteProperties(LogEvent logEvent, TextWriter output)
    {
        foreach (var item in logEvent.Properties)
        {
            output.WriteLine($"  {item.Key}: {item.Value}");
        }
    }

    private void WriteException(LogEvent logEvent, TextWriter output)
    {
        var detailLevel = _options.ExceptionDetailByLevel.TryGetValue(logEvent.Level, out var levelDetail)
            ? levelDetail
            : _options.ExceptionDetail;

        var ex = logEvent.Exception;
        if (ex == null)
        {
            // No exception to write about.
            return;
        }

        switch (detailLevel)
        {
            case TextFormatterOptions.ExceptionDetailLevel.None:
                return;
            case TextFormatterOptions.ExceptionDetailLevel.MessageOnly:
                output.WriteLine($"  Exception: {ex.Message}");
                return;
            case TextFormatterOptions.ExceptionDetailLevel.TypeAndMessage:
                output.WriteLine($"  Exception: {ex.GetType().Name}: {ex.Message}");
                return;
            case TextFormatterOptions.ExceptionDetailLevel.Full:
                WriteFullException(ex, output);
                return;
        }
    }

    private static void WriteFullException(Exception ex, TextWriter output)
    {
        output.WriteLine($"  Exception: {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException != null)
        {
            output.WriteLine($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            output.WriteLine($"  Stack: {ex.StackTrace}");
        }
    }

    private static string FormatLevel(LogEventLevel level) =>
        level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FTL",
            _ => level.ToString()
        };
}
