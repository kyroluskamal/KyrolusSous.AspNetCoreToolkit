namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusCacheLoggingObserverTests
{
    [Fact(DisplayName = "KyrolusCacheLoggingObserver: Respects filter flags and logs configured events")]
    public async Task LoggingObserver_RespectsFilters()
    {
        var logger = Substitute.For<ILogger<KyrolusCacheLoggingObserver>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);

        var options = new KyrolusCacheLoggingObserverOptions
        {
            LogHits = false,
            LogMisses = true,
            LogLevel = LogLevel.Information
        };

        var observer = new KyrolusCacheLoggingObserver(logger, options);

        // 1. Hit -> Should NOT log
        await observer.OnObservationAsync(new KyrolusCacheObserverContext(
            Key: "user:1",
            Operation: KyrolusCacheOperation.Get,
            Observation: KyrolusCacheObservation.Hit,
            ValueType: typeof(string),
            Duration: TimeSpan.FromMilliseconds(1),
            Region: null,
            TenantId: null,
            Exception: null));

        logger.Received(0).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        // 2. Miss -> MUST log
        await observer.OnObservationAsync(new KyrolusCacheObserverContext(
            Key: "user:1",
            Operation: KyrolusCacheOperation.Get,
            Observation: KyrolusCacheObservation.Miss,
            ValueType: typeof(string),
            Duration: TimeSpan.FromMilliseconds(1),
            Region: null,
            TenantId: null,
            Exception: null));

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
