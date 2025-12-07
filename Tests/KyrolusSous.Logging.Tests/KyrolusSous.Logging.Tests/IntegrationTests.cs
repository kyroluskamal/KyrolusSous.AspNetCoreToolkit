
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using static KyrolusSous.Logging.LoggingOptions;

namespace KyrolusSous.Logging.Tests;

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
    private bool _disposed = false;

    public LoggingIntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        var appEnvironment = _factory.Services.GetRequiredService<IHostEnvironment>();
        _logDirectory = Path.Combine(appEnvironment.ContentRootPath, "Logs");
        CleanupLogs();
    }

    protected void CleanupLogs()
    {
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, true);
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
    [Fact(DisplayName = "Should creates and writes to the default file sink")]
    public async Task Build_WithDefaultOptions_CreatesAndWritesToDefaultFileSink()
    {
        // Arrange
        var factory1 = new WebApplicationFactory<Program>();
        var client = factory1.CreateClient();

        // Act
        await client.GetAsync("/");
        await factory1.DisposeAsync();
        // ---------------------

        // Assert
        Directory.Exists(_logDirectory).ShouldBeTrue();

        var logFiles = Directory.GetFiles(_logDirectory, "log-*.txt");
        logFiles.ShouldHaveSingleItem();

        var fileContent = await ReadFileWithRetryAsync(logFiles.Single());
        fileContent.ShouldContain("Integration Test: Information Message");
    }
    [Fact(DisplayName = "Should not create default log file when sinks are cleared")]
    public async Task Build_WithClearDefaultSinks_DoesNotCreateDefaultLogFile()
    {
        // Arrange
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

        // Act
        await client.GetAsync("/");

        // Assert
        Directory.Exists(_logDirectory).ShouldBeFalse("The 'Logs' directory should NOT be created when default sinks are cleared.");
    }

    [Fact]
    public async Task Build_WithCustomFileSinkOptions_WritesToCustomPath()
    {
        // Arrange
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
                    Directory.Delete(customLogPath, true);
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

        // Act
        await client.GetAsync("/");
        await factory2.DisposeAsync();

        // Assert
        Directory.Exists(customLogPath).ShouldBeTrue();
        var logFiles = Directory.GetFiles(customLogPath!, "custom-log*.txt");
        logFiles.ShouldHaveSingleItem();

        var fileContent = await ReadFileWithRetryAsync(logFiles.Single());
        fileContent.ShouldContain("Integration Test: Information Message");

        // Cleanup
        Directory.Delete(customLogPath, true);
    }

    /// <summary>
    /// Helper method to read a file with a short retry mechanism to handle file lock race conditions.
    /// </summary>
    private static async Task<string> ReadFileWithRetryAsync(string filePath, int retries = 5, int delayMs = 100)
    {
        for (int i = 0; i < retries; i++)
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
    [Fact]
    public async Task Build_WithCustomSinkType_UsesTheCustomSink()
    {
        // Arrange
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
        // Act
        await client.GetAsync("/");
        // Assert
        var loggedEvent = InMemoryTestSink.Events.FirstOrDefault(e => e.MessageTemplate.Text.Contains("Integration Test: Information Message, endpoint '/' was hit."));

        loggedEvent.ShouldNotBeNull("An event containing the test message should have been logged.");
    }
    [Fact]
    public async Task Build_WithSinkOptionsAsDictionary_CreatesAndWritesToFileSink()
    {
        // Arrange

        string? customLogPath = null;
        if (Directory.Exists(customLogPath))
        {
            Directory.Delete(customLogPath, true);
        }

        var factory3 = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var serviceProvider = services.BuildServiceProvider();
                var appEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
                customLogPath = Path.Combine(appEnvironment.ContentRootPath, "DictionaryLogs");
                services.PostConfigure<LoggingOptions>(options =>
                {
                    // We start clean for this test
                    options.Sinks.Clear();
                    options.Sinks.Add(new SinkConfiguration
                    {
                        CommonType = CommonSinkType.File,

                        // --- THIS IS THE KEY PART ---
                        // We are providing the parameters as a dictionary,
                        // simulating an advanced user.
                        SinkOptions = new Dictionary<string, object?>
                        {
                        // Note: Keys must match the parameter names of the target method
                        { "path", Path.Combine(customLogPath, "dict-log.txt") },
                        { "rollingInterval", RollingInterval.Day },
                        { "outputTemplate", options.DefaultOutputTemplate }
                        }
                    });
                });
            });
        });

        var client = factory3.CreateClient();

        // Act
        await client.GetAsync("/");
        await factory3.DisposeAsync();

        // Assert
        Directory.Exists(customLogPath).ShouldBeTrue("The directory should be created based on dictionary configuration.");

        var logFiles = Directory.GetFiles(customLogPath, "dict-log*.txt");
        logFiles.ShouldHaveSingleItem("The log file should be created using parameters from the dictionary.");

        // Cleanup
        Directory.Delete(customLogPath, true);
    }
    /// <summary>
    /// A helper method that polls the file system until a file matching the pattern is found,
    /// or until the timeout is reached.
    /// </summary>
    // private static async Task<FileInfo?> WaitForFileToExistAsync(string directory, string pattern, TimeSpan timeout)
    // {
    //     var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    //     while (stopwatch.Elapsed < timeout)
    //     {
    //         if (Directory.Exists(directory))
    //         {
    //             // Directory.GetFiles can be replaced with DirectoryInfo if it causes issues.
    //             var files = Directory.GetFiles(directory, pattern);
    //             if (files.Length != 0)
    //             {
    //                 return new FileInfo(files[0]);
    //             }
    //         }
    //         await Task.Delay(100); // Wait for 100ms before checking again.
    //     }
    //     return null;
    // }
}
