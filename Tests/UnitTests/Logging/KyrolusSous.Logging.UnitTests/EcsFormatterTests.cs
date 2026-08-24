using System.Diagnostics;
using System.Text.Json;
using KyrolusSous.Logging.Serilog.Formatters;
using Serilog.Events;
using Serilog.Parsing;

namespace KyrolusSous.Logging.UnitTests;

public class EcsFormatterTests
{
    [Fact(DisplayName = "EcsJsonFormatter: Formats standard log event into ECS v1.12 JSON structure")]
    public void EcsJsonFormatter_FormatsLogEvent_Successfully()
    {
        var formatter = new KyrolusEcsJsonFormatter();
        var templateParser = new MessageTemplateParser();
        var template = templateParser.Parse("Processing order {OrderId} for user {UserId}");

        var properties = new List<LogEventProperty>
        {
            new("OrderId", new ScalarValue(1001)),
            new("UserId", new ScalarValue("USR-99")),
            new("SourceContext", new ScalarValue("MyService.OrderProcessor"))
        };

        var exception = new InvalidOperationException("Payment failed with token=secret123");
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            exception,
            template,
            properties);

        using var writer = new StringWriter();
        formatter.Format(logEvent, writer);

        var jsonOutput = writer.ToString();
        jsonOutput.ShouldNotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(jsonOutput);
        var root = doc.RootElement;

        root.GetProperty("@timestamp").GetString().ShouldNotBeNullOrEmpty();
        root.GetProperty("ecs").GetProperty("version").GetString().ShouldBe("1.12.0");
        root.GetProperty("log").GetProperty("level").GetString().ShouldBe("error");
        root.GetProperty("log").GetProperty("logger").GetString().ShouldBe("MyService.OrderProcessor");
        root.GetProperty("error").GetProperty("type").GetString().ShouldBe(typeof(InvalidOperationException).FullName);
        root.GetProperty("error").GetProperty("message").GetString()!.ShouldContain("token=***");
        root.GetProperty("labels").GetProperty("OrderId").GetString().ShouldBe("1001");
    }

    [Fact(DisplayName = "EcsJsonFormatter: Formats log event with ambient activity trace ID and no exception")]
    public void EcsJsonFormatter_WithActivity_FormatsTraceAndSpan()
    {
        var activity = new Activity("OrderWorkflow").Start();
        try
        {
            var formatter = new KyrolusEcsJsonFormatter();
            var template = new MessageTemplateParser().Parse("Activity log event");
            var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, template, []);

            using var writer = new StringWriter();
            formatter.Format(logEvent, writer);

            using var doc = JsonDocument.Parse(writer.ToString());
            var root = doc.RootElement;

            root.GetProperty("log").GetProperty("level").GetString().ShouldBe("info");
            root.GetProperty("trace").GetProperty("id").GetString().ShouldBe(activity.TraceId.ToString());
            root.GetProperty("span").GetProperty("id").GetString().ShouldBe(activity.SpanId.ToString());
        }
        finally
        {
            activity.Stop();
        }
    }
}
