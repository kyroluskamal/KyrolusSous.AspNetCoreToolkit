namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusCacheObserversTests
{
    [Fact(DisplayName = "KyrolusNullCacheObserver: Instance should execute without throwing")]
    public async Task NullObserver_ExecutesSafely()
    {
        var observer = KyrolusNullCacheObserver.Instance;
        var context = new KyrolusCacheObserverContext(
            Key: "user:1",
            Operation: KyrolusCacheOperation.Get,
            Observation: KyrolusCacheObservation.Hit,
            ValueType: typeof(string),
            Duration: TimeSpan.FromMilliseconds(5),
            Region: "region1",
            TenantId: "tenant1",
            Exception: null);

        await observer.OnObservationAsync(context);
        context.Key.ShouldBe("user:1");
        context.Operation.ShouldBe(KyrolusCacheOperation.Get);
        context.Observation.ShouldBe(KyrolusCacheObservation.Hit);
    }
}
