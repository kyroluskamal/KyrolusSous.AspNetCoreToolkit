namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusCacheInstrumentationTests
{
    [Fact(DisplayName = "KyrolusCacheInstrumentation: Recording metrics should execute without errors")]
    public void Instrumentation_RecordMethods_ExecuteWithoutErrors()
    {
        KyrolusCacheInstrumentation.ActivitySourceName.ShouldBe("KyrolusSous.Caching");
        KyrolusCacheInstrumentation.MeterName.ShouldBe("KyrolusSous.Caching");

        KyrolusCacheInstrumentation.RecordHit(KyrolusCacheOperation.Get, "redis", 1);
        KyrolusCacheInstrumentation.RecordMiss(KyrolusCacheOperation.Get, "redis", 1);
        KyrolusCacheInstrumentation.RecordSet(KyrolusCacheOperation.Set, "redis", 1);
        KyrolusCacheInstrumentation.RecordRemove(KyrolusCacheOperation.Remove, "redis", 1);
        KyrolusCacheInstrumentation.RecordError(KyrolusCacheOperation.Get, "redis");
        KyrolusCacheInstrumentation.RecordLockAcquired("redis");
        KyrolusCacheInstrumentation.RecordLockFailed("redis");
        KyrolusCacheInstrumentation.RecordLatency(KyrolusCacheOperation.Get, "redis", TimeSpan.FromMilliseconds(2.5));
        KyrolusCacheInstrumentation.RecordLockWait("redis", TimeSpan.FromMilliseconds(15.0));
    }
}
