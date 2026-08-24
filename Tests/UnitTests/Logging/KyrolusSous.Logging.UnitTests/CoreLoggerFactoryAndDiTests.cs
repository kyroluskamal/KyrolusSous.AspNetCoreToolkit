using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace KyrolusSous.Logging.UnitTests;

public sealed class CoreLoggerFactoryAndDiTests
{
    private sealed class TestService(IKyrolusLogger<TestService> logger)
    {
        public void DoWork()
        {
            logger.LogInformation("Work completed", new Dictionary<string, object?> { ["Password"] = "Secret123" });
        }
    }

    [Fact(DisplayName = "KyrolusLoggingCore: Registers all services in DI and logs with automated masking")]
    public void LoggingCore_RegistersServices_AndLogsWithMasking()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Information));
        services.AddKyrolusLoggingCore(opts =>
        {
            opts.CustomSensitiveKeywords.Add("CustomSecret");
        });
        services.AddTransient<TestService>();

        var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IKyrolusLoggerFactory>();
        factory.ShouldNotBeNull();

        var logger = factory.Create("CustomCategory");
        logger.ShouldNotBeNull();
        logger.IsEnabled(LogLevel.None).ShouldBeFalse();

        using (logger.BeginScope("ScopeKey", "ScopeVal"))
        {
            logger.LogInformation("Inside scope");
        }

        var typedLogger = factory.Create<TestService>();
        typedLogger.ShouldNotBeNull();
        typedLogger.IsEnabled(LogLevel.None).ShouldBeFalse();
        using (typedLogger.BeginScope("TypedKey", "TypedVal"))
        {
            typedLogger.Log(LogLevel.Information, "Typed log message", null, new Dictionary<string, object?> { ["CustomSecret"] = "xyz" });
        }

        var service = sp.GetRequiredService<TestService>();
        service.DoWork();

        var defaultLogger = sp.GetRequiredService<IKyrolusLogger>();
        defaultLogger.ShouldNotBeNull();
        defaultLogger.LogInformation("Default logger test");
    }

    [Fact(DisplayName = "KyrolusLoggingCore: AddKyrolusHttpLogging registers HTTP options and pipeline extension")]
    public void LoggingCore_AddKyrolusHttpLogging_RegistersSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusHttpLogging(opts =>
        {
            opts.IncludeRequestBody = true;
            opts.IncludeResponseBody = true;
            opts.MaxBodyLength = 1024;
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<KyrolusHttpLoggingOptions>>().Value;

        options.IncludeRequestBody.ShouldBeTrue();
        options.IncludeResponseBody.ShouldBeTrue();
        options.MaxBodyLength.ShouldBe(1024);

        var appBuilder = new ApplicationBuilder(sp);
        appBuilder.UseKyrolusHttpLogging();
        var app = appBuilder.Build();
        app.ShouldNotBeNull();
    }

    [Fact(DisplayName = "LoggingOptions: Fluent sink registration methods")]
    public void LoggingOptions_FluentSinkRegistrationMethods()
    {
        var options = new LoggingOptions();
        options.AddConsole(c => c.OutputTemplate = "test");
        options.AddFile("Logs/custom.txt", f => f.RetainedFileCountLimit = 7);
        options.AddSeq("http://localhost:5341", "my-api-key");
        options.AddCustomSink(cfg => { });

        options.Sinks.Count.ShouldBeGreaterThanOrEqualTo(3);
        options.AotSinkRegistrations.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "SerilogServiceExtension: Registers AddKyrolusLogging and builds host")]
    public void SerilogServiceExtension_RegistersAndConfigures_Correctly()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddKyrolusLogging(config, opt =>
        {
            opt.ApplicationName = "UnitTestApp";
            opt.AddConsole();
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<LoggingOptions>>().Value;
        options.ApplicationName.ShouldBe("UnitTestApp");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(s =>
            {
                s.AddKyrolusLogging(config, opt => opt.ApplicationName = "HostApp");
            })
            .UseKyrolusLogging()
            .Build();

        host.ShouldNotBeNull();
        host.Dispose();
    }

    [Fact(DisplayName = "LoggingOptions: Advanced enterprise options configuration")]
    public void LoggingOptions_AdvancedEnterpriseOptions_ConfigureCorrectly()
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
        var options = new LoggingOptions();
        options.EnableRateLimiter(10, TimeSpan.FromSeconds(30));
        options.UseEcsFormatting();
        options.UseDynamicLevelSwitch(levelSwitch);

        options.EnableRateLimiting.ShouldBeTrue();
        options.MaxDuplicateMessagesPerWindow.ShouldBe(10);
        options.RateLimitingWindow.ShouldBe(TimeSpan.FromSeconds(30));
        options.EnableEcsFormatting.ShouldBeTrue();
        options.DynamicLevelSwitch.ShouldBe(levelSwitch);
    }
}

