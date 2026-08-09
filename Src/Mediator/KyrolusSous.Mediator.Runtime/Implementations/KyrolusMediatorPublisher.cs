using KyrolusSous.Mediator.Runtime.GeneratorIntegration;

namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Concrete implementation of <see cref="IKyrolusMediatorPublisher"/>.
/// Resolves every registered handler for a notification and runs them under the configured
/// strategy, isolating each so one failure cannot stop the rest.
/// </summary>
/// <remarks>
/// Handler calls come from <see cref="IKyrolusNotificationDispatchSource"/> and are never found
/// here. The generator supplies a source that names every notification it saw;
/// <c>KyrolusSous.Mediator.Reflection</c> supplies one that closes
/// <c>INotificationHandler&lt;&gt;</c> on demand. Keeping both out of this class is what lets the
/// assembly be published ahead of time.
/// </remarks>
public sealed class KyrolusMediatorPublisher : IKyrolusMediatorPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IKyrolusNotificationPublishStrategy _publishStrategy;
    private readonly IKyrolusNotificationDispatchSource? _dispatchSource;

    /// <param name="serviceProvider">The service provider instance used to resolve notification handlers.</param>
    /// <param name="publishStrategy">Controls whether handlers run in parallel or sequentially.</param>
    /// <exception cref="ArgumentNullException">Thrown if serviceProvider or publishStrategy is null.</exception>
    public KyrolusMediatorPublisher(
        IServiceProvider serviceProvider,
        IKyrolusNotificationPublishStrategy publishStrategy)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _publishStrategy = publishStrategy ?? throw new ArgumentNullException(nameof(publishStrategy));
        _dispatchSource = serviceProvider.GetService<IKyrolusNotificationDispatchSource>();
    }

    /// <inheritdoc />
    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    => await PublishAsync(notification, null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task PublishAsync(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification is not INotification typed)
            throw new ArgumentException(
                $"[KyrolusMediator] {notification.GetType().FullName} does not implement {nameof(INotification)}.",
                nameof(notification));

        await PublishAsync(typed, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync(INotification notification, IKyrolusNotificationPublishStrategy? strategy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var effectiveStrategy = strategy ?? _publishStrategy;
        var notificationType = notification.GetType();

        var source = _dispatchSource ?? throw new InvalidOperationException(
            "[KyrolusMediator] No notification dispatch source is registered. Reference " +
            "KyrolusSous.Mediator.Generator and call AddKyrolusMediatorNotificationHandlers(), or " +
            "reference KyrolusSous.Mediator.Reflection and call AddKyrolusMediatorReflection().");

        // Null means the source has never seen this notification type, which is not the same as
        // having seen it and found no handler. Both end up doing nothing, but only the second is
        // normal, so they are not collapsed into one branch.
        var invocations = source.CreateHandlerInvocations(notification, _serviceProvider);
        if (invocations is null || invocations.Count == 0) return;

        // Handlers may run concurrently (the default strategy is parallel), so this must be a
        // thread-safe collection - List<T>.Add from several threads can lose an entry or tear
        // the backing array.
        ConcurrentBag<Exception> exceptions = [];

        // Each handler is isolated: notification handlers are independent subscribers, so one
        // throwing must not stop the others from running. Every failure is kept and reported
        // together once they have all had their turn.
        var guarded = invocations.Select(invocation => (Func<CancellationToken, Task>)(async ct =>
        {
            try
            {
                await invocation(ct).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }));

        await effectiveStrategy.PublishAsync(guarded, cancellationToken).ConfigureAwait(false);

        // If any exceptions were collected during the process, throw them all together.
        if (!exceptions.IsEmpty)
            throw new AggregateException($"[KyrolusMediator] One or more errors occurred while publishing notification '{notificationType.Name}'", exceptions);
    }
}
