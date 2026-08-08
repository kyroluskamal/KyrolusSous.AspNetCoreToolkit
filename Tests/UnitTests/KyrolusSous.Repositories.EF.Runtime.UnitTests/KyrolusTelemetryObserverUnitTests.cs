using KyrolusSous.Repositories.EF.Runtime.Observability;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class KyrolusTelemetryObserverUnitTests
{
    [Fact(DisplayName = "Telemetry observer writes log entry when logger is enabled and slow-threshold condition is met")]
    public async Task TelemetryObserver_Logs_WhenEnabled_AndThresholdMatched()
    {
        var logger = new RecordingTelemetryLogger();
        var observer = new KyrolusRepositoryTelemetryObserver(
            logger,
            new KyrolusRepositoryTelemetryObserverOptions
            {
                LogLevel = LogLevel.Information,
                SlowThreshold = TimeSpan.Zero,
                LogErrors = true
            });

        await observer.OnBeforeAsync("Telemetry.Logging", new { Kind = "before" });
        await observer.OnAfterAsync("Telemetry.Logging", new { Kind = "after" }, TimeSpan.FromMilliseconds(3), null);

        logger.LogCount.ShouldBe(1);
        logger.LastException.ShouldBeNull();
    }

    private sealed class RecordingTelemetryLogger : ILogger<KyrolusRepositoryTelemetryObserver>
    {
        private sealed class NoopScope : IDisposable
        {
            public static readonly IDisposable Instance = new NoopScope();
            public void Dispose() { }
        }

        public int LogCount { get; private set; }
        public Exception? LastException { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _ = logLevel;
            _ = eventId;
            _ = state;
            _ = formatter;
            LogCount++;
            LastException = exception;
        }
    }
}
