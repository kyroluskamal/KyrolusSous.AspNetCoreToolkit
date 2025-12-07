using System.Text;
using Microsoft.Extensions.Hosting;
using Moq;
using Serilog;
using Serilog.Parsing;
using Shouldly;
using static KyrolusSous.Logging.LoggingOptions;
using KyrolusSous.Logging.Theming;

namespace KyrolusSous.Logging.Tests;

public class FormatterAndStrictnessTests
{
    private static string FormatWithOptions(LogEventLevel level, Exception? ex, TextFormatterOptions options, IDictionary<string, LogEventPropertyValue>? extraProps = null)
    {
        var formatter = new CustomTextFormatter(options);
        var writer = new StringWriter(new StringBuilder());

        var template = new MessageTemplateParser().Parse("Hello {User}");
        var properties = new List<LogEventProperty>
        {
            new("User", new ScalarValue("alice")),
            new("SourceContext", new ScalarValue("MySource"))
        };

        if (extraProps != null)
        {
            foreach (var kvp in extraProps)
            {
                properties.Add(new LogEventProperty(kvp.Key, kvp.Value));
            }
        }

        var logEvent = new LogEvent(DateTimeOffset.UtcNow, level, ex, template, properties);
        formatter.Format(logEvent, writer);
        return writer.ToString();
    }

    [Fact]
    public void Formatter_Should_Honor_Visibility_Settings()
    {
        var options = new TextFormatterOptions
        {
            UseColors = false,
            ShowProperties = false,
            ShowSourceContext = false,
            ShowException = true
        };

        var output = FormatWithOptions(LogEventLevel.Information, null, options);

        output.ShouldContain("[INF]");
        output.ShouldContain("Hello");
        output.ShouldContain("alice");
        output.ShouldNotContain("SourceContext");
        output.ShouldNotContain("User:");
        output.ShouldNotContain("\u001b"); // no ANSI codes when UseColors = false
    }

    [Fact]
    public void Formatter_Should_Apply_Exception_Detail_Per_Level()
    {
        var options = new TextFormatterOptions
        {
            UseColors = false,
            ExceptionDetail = TextFormatterOptions.ExceptionDetailLevel.MessageOnly,
            ExceptionDetailByLevel =
            {
                [LogEventLevel.Error] = TextFormatterOptions.ExceptionDetailLevel.Full,
                [LogEventLevel.Fatal] = TextFormatterOptions.ExceptionDetailLevel.Full
            }
        };

        var infoEx = new InvalidOperationException("info message");
        var errorEx = new InvalidOperationException("error message", new Exception("inner"));

        var infoOutput = FormatWithOptions(LogEventLevel.Information, infoEx, options);
        var errorOutput = FormatWithOptions(LogEventLevel.Error, errorEx, options);

        infoOutput.ShouldContain("Exception: info message");
        infoOutput.ShouldNotContain("InvalidOperationException"); // MessageOnly

        errorOutput.ShouldContain("InvalidOperationException: error message"); // TypeAndMessage
        errorOutput.ShouldContain("Inner: Exception: inner");
    }

    [Fact]
    public void Build_Should_Throw_When_Package_Missing_And_Strict()
    {
        var options = new LoggingOptions
        {
            ThrowIfPackageMissing = true,
            Sinks =
            [
                new SinkConfiguration { SinkMethodName = "MissingMethod", SinkPackageName = "Missing.Package" }
            ]
        };

        var loggerConfig = new LoggerConfiguration();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());

        Should.Throw<InvalidOperationException>(() => LoggerConfigurationBuilder.Build(loggerConfig, options, env.Object));
    }

    [Fact]
    public void Build_Should_Skip_When_Package_Missing_And_Not_Strict()
    {
        var options = new LoggingOptions
        {
            ThrowIfPackageMissing = false,
            Sinks =
            [
                new SinkConfiguration { SinkMethodName = "MissingMethod", SinkPackageName = "Missing.Package" }
            ]
        };

        var loggerConfig = new LoggerConfiguration();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());

        Should.NotThrow(() => LoggerConfigurationBuilder.Build(loggerConfig, options, env.Object));
    }
}
