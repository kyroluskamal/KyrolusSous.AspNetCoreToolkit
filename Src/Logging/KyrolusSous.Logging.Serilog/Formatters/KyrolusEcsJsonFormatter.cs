using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KyrolusSous.Logging.Core.Exceptions;
using Serilog.Events;
using Serilog.Formatting;

namespace KyrolusSous.Logging.Serilog.Formatters;

/// <summary>
/// Formats log events into Elastic Common Schema (ECS) v1.12+ compliant JSON output.
/// </summary>
public sealed class KyrolusEcsJsonFormatter : ITextFormatter
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false };
    private readonly KyrolusExceptionSanitizer _exceptionSanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusEcsJsonFormatter"/> class.
    /// </summary>
    /// <param name="exceptionSanitizer">Optional exception sanitizer instance.</param>
    public KyrolusEcsJsonFormatter(KyrolusExceptionSanitizer? exceptionSanitizer = null)
    {
        _exceptionSanitizer = exceptionSanitizer ?? new KyrolusExceptionSanitizer();
    }

    /// <inheritdoc/>
    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        using var memoryStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memoryStream, WriterOptions))
        {
            writer.WriteStartObject();

            // 1. @timestamp
            writer.WriteString("@timestamp", logEvent.Timestamp.UtcDateTime.ToString("o"));

            // 2. ecs.version
            writer.WriteStartObject("ecs");
            writer.WriteString("version", "1.12.0");
            writer.WriteEndObject();

            // 3. log object
            writer.WriteStartObject("log");
            writer.WriteString("level", FormatLevel(logEvent.Level));
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContextVal))
            {
                writer.WriteString("logger", sourceContextVal.ToString().Trim('"'));
            }
            writer.WriteEndObject();

            // 4. message
            using var msgWriter = new StringWriter();
            logEvent.RenderMessage(msgWriter);
            writer.WriteString("message", _exceptionSanitizer.SanitizeMessage(msgWriter.ToString()));

            // 5. Distributed Tracing / W3C Context
            var traceId = Activity.Current?.TraceId.ToString();
            var spanId = Activity.Current?.SpanId.ToString();

            if (!string.IsNullOrEmpty(traceId))
            {
                writer.WriteStartObject("trace");
                writer.WriteString("id", traceId);
                writer.WriteEndObject();
            }

            if (!string.IsNullOrEmpty(spanId))
            {
                writer.WriteStartObject("span");
                writer.WriteString("id", spanId);
                writer.WriteEndObject();
            }

            // 6. Error object (if Exception present)
            if (logEvent.Exception is not null)
            {
                writer.WriteStartObject("error");
                writer.WriteString("type", logEvent.Exception.GetType().FullName);
                writer.WriteString("message", _exceptionSanitizer.SanitizeMessage(logEvent.Exception.Message));
                if (logEvent.Exception.StackTrace is not null)
                {
                    writer.WriteString("stack_trace", logEvent.Exception.StackTrace);
                }
                writer.WriteEndObject();
            }

            // 7. Labels / Custom properties
            writer.WriteStartObject("labels");
            foreach (var (key, val) in logEvent.Properties)
            {
                if (key == "SourceContext")
                {
                    continue;
                }

                writer.WriteString(key, val.ToString().Trim('"'));
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        output.WriteLine(Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length));
    }

    private static string FormatLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "trace",
        LogEventLevel.Debug => "debug",
        LogEventLevel.Information => "info",
        LogEventLevel.Warning => "warn",
        LogEventLevel.Error => "error",
        LogEventLevel.Fatal => "fatal",
        _ => "info"
    };
}
