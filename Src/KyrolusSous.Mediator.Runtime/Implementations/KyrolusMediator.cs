namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Unified mediator implementation that composes sender and publisher.
/// </summary>
public sealed class KyrolusMediator(
    IKyrolusMediatorSender sender,
    IKyrolusMediatorPublisher publisher) : IKyrolusMediator, IMediator
{
    private readonly IKyrolusMediatorSender _sender = sender ?? throw new ArgumentNullException(nameof(sender), "[KyrolusMediator] It looks like you are trying to use the KyrolusMediator without registering the sender service. Please ensure that you have registered the IKyrolusMediatorSender implementation in your dependency injection container.");
    private readonly IKyrolusMediatorPublisher _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher), "[KyrolusMediator] It looks like you are trying to use the KyrolusMediator without registering the publisher service. Please ensure that you have registered the IKyrolusMediatorPublisher implementation in your dependency injection container.");

    public Task<TResponse> SendAsync<TResponse>(IKyrolusQuery<TResponse> query, CancellationToken cancellationToken = default)
        => _sender.SendAsync(query, cancellationToken);

    public Task<TResponse> SendAsync<TResponse>(IKyrolusRequest<TResponse> request, CancellationToken cancellationToken = default)
        => _sender.SendAsync(request, cancellationToken);

    public Task SendAsync(IKyrolusCommand command, CancellationToken cancellationToken = default)
        => _sender.SendAsync(command, cancellationToken);

    public Task<TResponse> SendAsync<TResponse>(IKyrolusCommand<TResponse> command, CancellationToken cancellationToken = default)
        => _sender.SendAsync(command, cancellationToken);

    public Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(notification, cancellationToken);

    public Task PublishAsync(INotification notification, IKyrolusNotificationPublishStrategy? strategy, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(notification, strategy, cancellationToken);

    public IAsyncEnumerable<TResponse> StreamAsync<TResponse>(IKyrolusStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => _sender.StreamAsync(request, cancellationToken);

    public Task<object?> SendAsync(object request, CancellationToken cancellationToken = default)
        => _sender.SendAsync(request, cancellationToken);

    public IAsyncEnumerable<object?> StreamAsync(object request, CancellationToken cancellationToken = default)
        => _sender.StreamAsync(request, cancellationToken);

    public Task PublishAsync(object notification, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(notification, cancellationToken);
}
