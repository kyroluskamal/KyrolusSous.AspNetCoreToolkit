namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusNotificationPublishStrategy
{
    Task PublishAsync(IEnumerable<Func<CancellationToken, Task>> handlers, CancellationToken cancellationToken);
}
