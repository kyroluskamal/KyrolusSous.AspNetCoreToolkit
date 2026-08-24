namespace KyrolusSous.Logging.UnitTests;

public sealed class TimedOperationAndExtensionsTests
{
    private sealed class CapturingLogger : IKyrolusLogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, object?>? Properties)> Entries { get; } = [];

        public bool IsEnabled(LogLevel level) => true;

        public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> values) => null;

        public void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        {
            Entries.Add((level, message, exception, properties));
        }
    }

    [Fact(DisplayName = "KyrolusTimedOperation: Logs completion upon disposal")]
    public void TimedOperation_LogsCompletion()
    {
        var logger = new CapturingLogger();

        using (logger.BeginTimedOperation("DatabaseQuery", warnIfExceedsMs: 5000))
        {
            Thread.Sleep(10);
        }

        logger.Entries.Count.ShouldBe(1);
        var entry = logger.Entries[0];
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldContain("DatabaseQuery");
        entry.Properties.ShouldNotBeNull();
        entry.Properties["OperationName"].ShouldBe("DatabaseQuery");
        entry.Properties["ExceededThreshold"].ShouldBe(false);
    }

    [Fact(DisplayName = "KyrolusTimedOperation: Emits warning when exceeding duration threshold")]
    public void TimedOperation_EmitsWarning_WhenExceedingThreshold()
    {
        var logger = new CapturingLogger();

        using (logger.BeginTimedOperation("HeavyCalculation", warnIfExceedsMs: 1))
        {
            Thread.Sleep(20);
        }

        logger.Entries.Count.ShouldBe(1);
        var entry = logger.Entries[0];
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("Slow operation detected");
        entry.Properties.ShouldNotBeNull();
        entry.Properties["ExceededThreshold"].ShouldBe(true);
    }

    [Fact(DisplayName = "KyrolusLoggerExtensions: Rich logging helper methods")]
    public void LoggerExtensions_LogAtAllLevels()
    {
        var logger = new CapturingLogger();

        logger.LogTrace("Trace message");
        logger.LogDebug("Debug message");
        logger.LogInformation("Info message");
        logger.LogWarning("Warn message");
        logger.LogError("Error message", new InvalidOperationException("boom"));
        logger.LogCritical("Critical message", new Exception("fatal"));

        logger.Entries.Count.ShouldBe(6);
        logger.Entries[0].Level.ShouldBe(LogLevel.Trace);
        logger.Entries[1].Level.ShouldBe(LogLevel.Debug);
        logger.Entries[2].Level.ShouldBe(LogLevel.Information);
        logger.Entries[3].Level.ShouldBe(LogLevel.Warning);
        logger.Entries[4].Level.ShouldBe(LogLevel.Error);
        logger.Entries[4].Exception.ShouldNotBeNull();
        logger.Entries[5].Level.ShouldBe(LogLevel.Critical);
        logger.Entries[5].Exception.ShouldNotBeNull();
    }
}
