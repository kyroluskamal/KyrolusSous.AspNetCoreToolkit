using System.Reflection;
using Microsoft.Extensions.Hosting;
using Moq;
using Shouldly;
using static KyrolusSous.Logging.LoggingOptions;

namespace KyrolusSous.Logging.Tests;

public class BuilderCoverageTests
{
    private static T InvokePrivate<T>(string methodName, params object[] args)
    {
        var method = typeof(LoggerConfigurationBuilder).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull($"Private static method '{methodName}' should exist.");
        var result = method!.Invoke(null, args);
        return (T)result!;
    }

    [Fact]
    public void ConvertOptionsToDictionary_Should_CamelCase_Properties()
    {
        var sample = new SampleOptions { OutputTemplate = "tpl", FileSizeLimitBytes = 1024 };

        var dict = InvokePrivate<Dictionary<string, object?>>("ConvertOptionsToDictionary", sample);

        dict.ShouldContainKey("outputTemplate");
        dict.ShouldContainKey("fileSizeLimitBytes");
        dict["outputTemplate"].ShouldBe("tpl");
        dict["fileSizeLimitBytes"].ShouldBe(1024L);
    }

    [Fact]
    public void PrepareSinkParameters_Should_Add_DefaultTemplate_And_Normalize_Path()
    {
        var parameters = new Dictionary<string, object?> { ["path"] = "Logs/app.txt" };
        var options = new LoggingOptions { DefaultOutputTemplate = "default" };
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns("C:\\root");

        InvokePrivate<object?>("PrepareSinkParameters", parameters, CommonSinkType.File, env.Object, options);

        parameters["outputTemplate"].ShouldBe("default");
        parameters["path"].ShouldBe(Path.Combine("C:\\root", "Logs/app.txt"));
    }

    [Fact]
    public void GetSinkKey_Should_Use_CommonType_MethodName_Or_CustomType()
    {
        var common = new SinkConfiguration { CommonType = CommonSinkType.Console };
        InvokePrivate<string>("GetSinkKey", common).ShouldBe("Console");

        var method = new SinkConfiguration { SinkMethodName = "CustomMethod", SinkPackageName = "Pkg" };
        InvokePrivate<string>("GetSinkKey", method).ShouldBe("CustomMethod");

        var custom = new SinkConfiguration { CustomType = typeof(TestSink) };
        InvokePrivate<string>("GetSinkKey", custom).ShouldBe(nameof(TestSink));
    }

    [Fact]
    public void Build_Should_Inject_Formatter_For_Console_Sink_When_Missing()
    {
        var options = new LoggingOptions
        {
            ThrowIfPackageMissing = false,
            Sinks =
            [
                new SinkConfiguration
                {
                    CommonType = CommonSinkType.Console,
                    SinkOptions = new LoggingOptions.ConsoleSinkOptions
                    {
                        Formatter = null
                    }
                }
            ]
        };

        var loggerConfig = new LoggerConfiguration();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());

        Should.NotThrow(() => LoggerConfigurationBuilder.Build(loggerConfig, options, env.Object));

        var sinkOptions = (LoggingOptions.ConsoleSinkOptions)options.Sinks[0].SinkOptions!;
        sinkOptions.Formatter.ShouldNotBeNull();
    }

    private class SampleOptions
    {
        public string OutputTemplate { get; set; } = string.Empty;
        public long FileSizeLimitBytes { get; set; } = 0;
    }
}
