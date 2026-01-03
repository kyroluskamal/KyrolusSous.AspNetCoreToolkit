namespace KyrolusSous.Caching.Abstractions;

public enum KyrolusCacheObservation
{
    Hit,
    Miss,
    Set,
    Remove,
    Exists,
    Error,
    LockAcquired,
    LockFailed
}

public sealed record KyrolusCacheObserverContext(
    string Key,
    KyrolusCacheOperation Operation,
    KyrolusCacheObservation Observation,
    Type? ValueType,
    TimeSpan? Duration,
    string? Region,
    string? TenantId,
    Exception? Exception);

public interface IKyrolusCacheObserver
{
    Task OnObservationAsync(KyrolusCacheObserverContext context);
}

public sealed class KyrolusNullCacheObserver : IKyrolusCacheObserver
{
    public static IKyrolusCacheObserver Instance { get; } = new KyrolusNullCacheObserver();

    public Task OnObservationAsync(KyrolusCacheObserverContext context) => Task.CompletedTask;
}
