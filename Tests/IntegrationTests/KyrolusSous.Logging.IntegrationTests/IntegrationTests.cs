using static KyrolusSous.Logging.Serilog.LoggingOptions;

namespace KyrolusSous.Logging.IntegrationTests;

public class InMemoryTestSink : ILogEventSink
{
    public static readonly List<LogEvent> Events = [];
    public void Emit(LogEvent logEvent)
    {
        Events.Add(logEvent);
    }
}

public class LoggingIntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    protected readonly WebApplicationFactory<Program> _factory;
    protected readonly string _logDirectory;
    private bool _disposed;

    public LoggingIntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        var appEnvironment = _factory.Services.GetRequiredService<IHostEnvironment>();
        _logDirectory = Path.Combine(appEnvironment.ContentRootPath, "Logs");
        CleanupLogs();
    }

    protected void CleanupLogs()
    {
        Log.CloseAndFlush();
        if (!Directory.Exists(_logDirectory))
        {
            return;
        }

        try
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    Directory.Delete(_logDirectory, true);
                    break;
                }
                catch (IOException) when (i < 4)
                {
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException) when (i < 4)
                {
                    Thread.Sleep(100);
                }
            }
        }
        catch (IOException)
        {
            // Swallow if the file is locked by an external process
        }
        catch (UnauthorizedAccessException)
        {
            // Swallow if the file is locked by an external process
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                CleanupLogs();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class IntegrationTests(WebApplicationFactory<Program> factory) : LoggingIntegrationTestBase(factory)
{
    [Fact(DisplayName = "Integration: default options should create and write to file sink")]
    public async Task Build_WithDefaultOptions_CreatesAndWritesToDefaultFileSink()
    {
        var start = DateTime.UtcNow;
        var factory1 = new WebApplicationFactory<Program>();
        var client = factory1.CreateClient();

        await client.GetAsync("/");
        await factory1.DisposeAsync();

        Directory.Exists(_logDirectory).ShouldBeTrue();

        var logFiles = Directory.GetFiles(_logDirectory, "log-*.txt")
            .Select(f => new FileInfo(f))
            .Where(f => f.LastWriteTimeUtc >= start.AddSeconds(-1))
            .ToList();
        logFiles.ShouldNotBeEmpty();

        var newest = logFiles.OrderByDescending(f => f.LastWriteTimeUtc).First();
        var fileContent = await ReadFileWithRetryAsync(newest.FullName);
        fileContent.ShouldContain("Integration Test: Information Message");
    }

    [Fact(DisplayName = "Integration: clearing sinks should not create default log file")]
    public async Task Build_WithClearDefaultSinks_DoesNotCreateDefaultLogFile()
    {
        var start = DateTime.UtcNow;
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<LoggingOptions>(options =>
                {
                    options.Sinks.Clear();
                });
            });
        }).CreateClient();

        await client.GetAsync("/");

        if (!Directory.Exists(_logDirectory))
        {
            return;
        }

        var recentFiles = Directory.GetFiles(_logDirectory, "log-*.txt")
            .Select(f => new FileInfo(f))
            .Where(f => f.LastWriteTimeUtc >= start.AddSeconds(-1))
            .ToList();
        recentFiles.ShouldBeEmpty("No new log files should be created when default sinks are cleared.");
    }

    [Fact(DisplayName = "Integration: custom file sink options should write to custom path")]
    public async Task Build_WithCustomFileSinkOptions_WritesToCustomPath()
    {
        var start = DateTime.UtcNow;
        string? customLogPath = null;
        var factory2 = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var serviceProvider = services.BuildServiceProvider();
                var appEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
                customLogPath = Path.Combine(appEnvironment.ContentRootPath, "MyCustomLogs");
                if (Directory.Exists(customLogPath))
                {
                    try
                    {
                        Directory.Delete(customLogPath, true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
                services.PostConfigure<LoggingOptions>(options =>
                {
                    var fileSinkConfig = options.Sinks.FirstOrDefault(s => s.CommonType == CommonSinkType.File);

                    if (fileSinkConfig?.SinkOptions is FileSinkOptions fileOptions)
                    {
                        fileOptions.Path = Path.Combine(customLogPath, "custom-log.txt");
                    }
                });
            });
        });

        var client = factory2.CreateClient();

        await client.GetAsync("/");
        await factory2.DisposeAsync();

        Directory.Exists(customLogPath).ShouldBeTrue();
        var logFiles = Directory.GetFiles(customLogPath!, "custom-log*.txt")
            .Select(f => new FileInfo(f))
            .Where(f => f.LastWriteTimeUtc >= start.AddSeconds(-1))
            .ToList();
        logFiles.ShouldNotBeEmpty();

        var newest = logFiles.OrderByDescending(f => f.LastWriteTimeUtc).First();
        var fileContent = await ReadFileWithRetryAsync(newest.FullName);
        fileContent.ShouldContain("Integration Test: Information Message");

        try
        {
            Directory.Delete(customLogPath, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task<string> ReadFileWithRetryAsync(string filePath, int retries = 5, int delayMs = 100)
    {
        for (var i = 0; i < retries; i++)
        {
            try
            {
                return await File.ReadAllTextAsync(filePath);
            }
            catch (IOException)
            {
                if (i == retries - 1) throw;
                await Task.Delay(delayMs);
            }
        }
        return string.Empty;
    }

    [Fact(DisplayName = "Integration: custom sink type should be used when configured")]
    public async Task Build_WithCustomSinkType_UsesTheCustomSink()
    {
        InMemoryTestSink.Events.Clear();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<LoggingOptions>(options =>
                {
                    options.Sinks.Clear();
                    options.Sinks.Add(new SinkConfiguration
                    {
                        CustomType = typeof(InMemoryTestSink)
                    });
                });
            });
        }).CreateClient();
        InMemoryTestSink.Events.Clear();

        await client.GetAsync("/");

        var loggedEvent = InMemoryTestSink.Events.FirstOrDefault(e => e.MessageTemplate.Text.Contains("Integration Test: Information Message, endpoint '/' was hit."));
        loggedEvent.ShouldNotBeNull("An event containing the test message should have been logged.");
    }

    [Fact(DisplayName = "Integration: sink options dictionary should create file sink")]
    public async Task Build_WithSinkOptionsAsDictionary_CreatesAndWritesToFileSink()
    {
        var start = DateTime.UtcNow;
        string? customLogPath = null;

        var factory3 = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var serviceProvider = services.BuildServiceProvider();
                var appEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
                customLogPath = Path.Combine(appEnvironment.ContentRootPath, "DictionaryLogs");
                if (Directory.Exists(customLogPath))
                {
                    Directory.Delete(customLogPath, true);
                }

                services.PostConfigure<LoggingOptions>(options =>
                {
                    options.Sinks.Clear();
                    options.Sinks.Add(new SinkConfiguration
                    {
                        CommonType = CommonSinkType.File,
                        SinkOptions = new Dictionary<string, object?>
                        {
                            { "path", Path.Combine(customLogPath, "dict-log.txt") },
                            { "rollingInterval", RollingInterval.Day },
                            { "outputTemplate", options.DefaultOutputTemplate }
                        }
                    });
                });
            });
        });

        var client = factory3.CreateClient();

        await client.GetAsync("/");
        await factory3.DisposeAsync();

        Directory.Exists(customLogPath).ShouldBeTrue("The directory should be created based on dictionary configuration.");

        var logFiles = Directory.GetFiles(customLogPath!, "dict-log*.txt")
            .Select(f => new FileInfo(f))
            .Where(f => f.LastWriteTimeUtc >= start.AddSeconds(-1))
            .ToList();
        logFiles.ShouldNotBeEmpty("The log file should be created using parameters from the dictionary.");

        try
        {
            Directory.Delete(customLogPath, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
