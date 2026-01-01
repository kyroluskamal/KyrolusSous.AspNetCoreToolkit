namespace KyrolusSous.Mediator.Runtime.Implementations;

public sealed class KyrolusSequentialNotificationPublishStrategy : IKyrolusNotificationPublishStrategy
{
    public async Task PublishAsync(IEnumerable<Func<CancellationToken, Task>> handlers, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            await handler(cancellationToken).ConfigureAwait(false);
        }
    }
}
