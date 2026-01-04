namespace KyrolusSous.DataProtection.Abstractions;

public interface IKyrolusKeyRingRefreshNotifier
{
    Task PublishAsync(
        KyrolusKeyRingRefreshSignal signal,
        CancellationToken cancellationToken = default);

    Task ListenAsync(
        Func<KyrolusKeyRingRefreshSignal, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);
}
