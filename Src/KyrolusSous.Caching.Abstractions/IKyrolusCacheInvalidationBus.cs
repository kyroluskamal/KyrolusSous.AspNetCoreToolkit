namespace KyrolusSous.Caching.Abstractions;

public interface IKyrolusCacheInvalidationBus
{
    Task PublishAsync(KyrolusCacheInvalidationMessage message, CancellationToken cancellationToken = default);
    IDisposable Subscribe(Func<KyrolusCacheInvalidationMessage, Task> handler);
}
