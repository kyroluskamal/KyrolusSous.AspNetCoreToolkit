namespace KyrolusSous.Mediator.Runtime.Implementations;

public sealed class KyrolusParallelNotificationPublishStrategy : IKyrolusNotificationPublishStrategy
{
    public Task PublishAsync(IEnumerable<Func<CancellationToken, Task>> handlers, CancellationToken cancellationToken)
    {
        var tasks = handlers.Select(handler => handler(cancellationToken));
        return Task.WhenAll(tasks);
    }
}
