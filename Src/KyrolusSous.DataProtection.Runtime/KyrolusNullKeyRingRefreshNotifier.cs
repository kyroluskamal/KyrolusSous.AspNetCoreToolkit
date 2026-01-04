using KyrolusSous.DataProtection.Abstractions;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusNullKeyRingRefreshNotifier : IKyrolusKeyRingRefreshNotifier
{
    public Task PublishAsync(
        KyrolusKeyRingRefreshSignal signal,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ListenAsync(
        Func<KyrolusKeyRingRefreshSignal, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
