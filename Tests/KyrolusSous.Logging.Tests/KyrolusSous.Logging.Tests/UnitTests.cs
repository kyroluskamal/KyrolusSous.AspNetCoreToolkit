global using Microsoft.Extensions.Hosting;
global using Moq;
global using Serilog;
global using Shouldly;
using Serilog.Context;
using static KyrolusSous.Logging.LoggingOptions;
namespace KyrolusSous.Logging.Tests;
#pragma warning disable S2094
public class NotAnEnricher { }
#pragma warning restore S2094
#pragma warning disable CS9113
public class EnricherWithNoDefaultConstructor(string name) : ILogEventEnricher
#pragma warning restore CS9113
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) { }
}
#pragma warning restore CS9113

public class LoggerUnitTests
{
    private readonly Mock<IHostEnvironment> _mockEnvironment;

    public LoggerUnitTests()
    {
        _mockEnvironment = new Mock<IHostEnvironment>();
        _mockEnvironment.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());
    }
    [Fact(DisplayName = "Should build logger configuration with default Enrichers")]
    public void Build_WithEmptyOptions_AppliesDefaultEnrichers()
    {
        var options = new LoggingOptions();
        var testSink = new TestSink();
        var loggerConfig = new LoggerConfiguration();
        LoggerConfigurationBuilder.Build(loggerConfig, options, _mockEnvironment.Object);

        var logger = loggerConfig.WriteTo.Sink(testSink).CreateLogger();
        logger.Information("Hello, World!");

        testSink.Events.ShouldNotBeEmpty();
        Assert.Equal("Hello, World!", testSink.Events[0].MessageTemplate.Text);

        var loggedEvent = testSink.Events[0];
        loggedEvent.Properties.ShouldContainKey("Application");
        loggedEvent.Properties.ShouldContainKey(CommonEnricherType.MachineName.ToString());
        loggedEvent.Properties.ShouldContainKey(CommonEnricherType.ProcessId.ToString());
        loggedEvent.Properties.ShouldContainKey(CommonEnricherType.ThreadId.ToString());
        loggedEvent.Properties.ShouldContainKey(CommonEnricherType.EnvironmentName.ToString());
    }

    [Fact(DisplayName = "Should build with fromlogcontext enabled and add properties fromContext")]
    public void Build_WithFromLogContextEnabled_AddsPropertiesFromContext()
    {
        // Arrange
        var options = new LoggingOptions();
        var testSink = new TestSink();
        var loggerConfig = new LoggerConfiguration();

        // Act
        LoggerConfigurationBuilder.Build(loggerConfig, options, _mockEnvironment.Object);
        var logger = loggerConfig.WriteTo.Sink(testSink).CreateLogger();

        using (LogContext.PushProperty("UserId", 123))
        using (LogContext.PushProperty("Action", "Login"))
        {
            logger.Information("User attempted to log in");
        }
        logger.Information("This message will not have the context properties");

        // Assert
        testSink.Events.Count.ShouldBe(2);

        var eventWithContext = testSink.Events[0];
        eventWithContext.Properties.ShouldContainKey("UserId");
        eventWithContext.Properties.ShouldContainKey("Action");
        eventWithContext.Properties["UserId"].ToString().ShouldBe("123");
        eventWithContext.Properties["Action"].ToString().ShouldBe("\"Login\"");

        var eventWithoutContext = testSink.Events[1];
        eventWithoutContext.Properties.ShouldNotContainKey("UserId");
        eventWithoutContext.Properties.ShouldNotContainKey("Action");
    }
    [Fact(DisplayName = "Should build logger configuration with advanced enricher options by name")]
    public void Build_WithAdvancedEnricherOptions_AppliesEnricherByName()
    {
        // Arrange
        var options = new LoggingOptions();
        var testSink = new TestSink();
        var loggerConfig = new LoggerConfiguration();
        options.Enrichers.Add(new EnricherConfiguration
        {
            MethodName = "WithAssemblyVersion",
            PackageName = "Serilog.Enrichers.AssemblyName"
        });

        // Act
        LoggerConfigurationBuilder.Build(loggerConfig, options, _mockEnvironment.Object);
        var logger = loggerConfig.WriteTo.Sink(testSink).CreateLogger();
        logger.Information("Testing advanced enricher by name");

        // Assert
        var loggedEvent = testSink.Events.ShouldHaveSingleItem();

        loggedEvent.Properties.ShouldContainKey("AssemblyVersion");

        var assemblyVersion = loggedEvent.Properties["AssemblyVersion"].ToString();
        assemblyVersion.ShouldNotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should build logger configuration with advanced enricher options by type")]
    public void Build_WithAdvancedEnricherOptionsByType_AppliesEnricherByType()
    {
        // Arrange
        var options = new LoggingOptions();
        var testSink = new TestSink();
        var loggerConfig = new LoggerConfiguration();

        options.Enrichers.Add(new EnricherConfiguration
        {
            CustomType = typeof(CorrelationIdEnricher)
        });

        var testCorrelationId = "test-id-12345";
        MyCorrelationIdContext.SetCorrelationId(testCorrelationId);

        // Act
        LoggerConfigurationBuilder.Build(loggerConfig, options, _mockEnvironment.Object);
        var logger = loggerConfig.WriteTo.Sink(testSink).CreateLogger();

        logger.Information("Testing custom enricher");

        // Assert
        testSink.Events.ShouldHaveSingleItem();

        var loggedEvent = testSink.Events.Single();
        loggedEvent.Properties.ShouldContainKey("CorrelationId");
        loggedEvent.Properties["CorrelationId"].ToString().ShouldBe($"\"{testCorrelationId}\"");
    }
    [Fact(DisplayName = "Should build logger configuration with minimum level overrides")]
    public void Build_OverrideMimimumLevel_SetsMinimumLevelToDebug()
    {
        // Arrange
        var options = new LoggingOptions
        {
            MinimumLevel = LogEventLevel.Information,
            MinimumLevelOverrides = new Dictionary<string, LogEventLevel>
            {
                { "MyTestContext", LogEventLevel.Debug },
            }
        };

        var testSink = new TestSink();
        var loggerConfig = new LoggerConfiguration();

        // Act
        LoggerConfigurationBuilder.Build(loggerConfig, options, _mockEnvironment.Object);
        var logger = loggerConfig.WriteTo.Sink(testSink).CreateLogger();
        var loggerForTestContext = logger.ForContext("SourceContext", "MyTestContext");
        var loggerForOtherContext = logger.ForContext("SourceContext", "SomeOther.Namespace");

        loggerForTestContext.Debug("Debug message from test context.");
        loggerForTestContext.Verbose("Verbose message from test context.");

        loggerForOtherContext.Debug("Debug message from other context.");
        loggerForOtherContext.Information("Info message from other context.");
        // Assert
        testSink.Events.Count.ShouldBe(2); // 1 from test context, 1 from other context
        testSink.Events[0].Level.ShouldBe(LogEventLevel.Debug); // From MyTestContext
        testSink.Events[0].Properties.ShouldContainKey("SourceContext"); // From SomeOther.Namespace
        testSink.Events[0].Properties["SourceContext"].ToString().ShouldContain("\"MyTestContext\""); // From MyTestContext
        testSink.Events[0].MessageTemplate.Text.ShouldBe("Debug message from test context.");
        testSink.Events[1].Level.ShouldBe(LogEventLevel.Information); // From SomeOther.Namespace
        testSink.Events[1].Properties.ShouldContainKey("SourceContext"); // From SomeOther.Namespace
        testSink.Events[1].Properties["SourceContext"].ToString().ShouldContain("\"SomeOther.Namespace\""); // From SomeOther.Namespace
        testSink.Events[1].MessageTemplate.Text.ShouldBe("Info message from other context.");
    }

    [Fact(DisplayName = "Should build logger configuration with custom enricher type that does not implement ILogEventEnricher")]
    public void Build_WithWrongCustomType_DoesNotThrowException()
    {
        // Arrange
        var options = new LoggingOptions();
        options.Enrichers.Add(new EnricherConfiguration
        {
            CustomType = typeof(NotAnEnricher)
        });
        var loggerConfig = new LoggerConfiguration();
        // Act
        Action act = () => LoggerConfigurationBuilder.Build(loggerConfig, options, _mockEnvironment.Object);
        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact(DisplayName = "Should build logger configuration with custom enricher missing default constructor")]
    public void Build_WithCustomEnricherMissingDefaultConstructor_DoesNotThrowException()
    {
        // Arrange
        var options = new LoggingOptions();
        options.Enrichers.Add(new EnricherConfiguration
        {
            CustomType = typeof(EnricherWithNoDefaultConstructor)
        });

        var loggerConfig = new LoggerConfiguration();
        loggerConfig.WriteTo.Console();
        // Act
        Action act = () => LoggerConfigurationBuilder.Build(loggerConfig, options, _mockEnvironment.Object);

        // Assert
        act.ShouldThrow<Exception>();
    }
}

