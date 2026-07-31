namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Concrete implementation of <see cref="IKyrolusMediatorPublisher"/>.
/// Resolves and invokes all registered notification handlers for a given notification,
/// handling exceptions from individual handlers gracefully and ensuring all handlers are attempted.
/// Uses reflection with caching for invocation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KyrolusMediatorPublisher"/> class.
/// </remarks>
/// <param name="serviceProvider">The service provider instance used to resolve notification handlers.</param>
/// <param name="publishStrategy">Controls whether handlers run in parallel or sequentially.</param>
/// <exception cref="ArgumentNullException">Thrown if serviceProvider is null.</exception>
public sealed class KyrolusMediatorPublisher(
    IServiceProvider serviceProvider,
    IKyrolusNotificationPublishStrategy publishStrategy) : IKyrolusMediatorPublisher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IKyrolusNotificationPublishStrategy _publishStrategy = publishStrategy ?? throw new ArgumentNullException(nameof(publishStrategy));
    // Cache MethodInfo for Handle methods to improve reflection performance.
    // Key: (concrete handler type, notification type). Both parts are required - one handler
    // class may implement INotificationHandler<> for several notifications, and keying on the
    // handler alone would hand back the Handle overload of whichever notification arrived first.
    private static readonly ConcurrentDictionary<(Type HandlerType, Type NotificationType), MethodInfo> s_handlerMethodCache = new();

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
        var handlerTypeDefinition = typeof(INotificationHandler<>);
        var handlerInterfaceType = handlerTypeDefinition.MakeGenericType(notificationType);

        // Resolve ALL registered handlers for this specific notification type from DI
        var handlers = _serviceProvider.GetServices(handlerInterfaceType).ToList();

        if (handlers.Count == 0) return;

        // Handlers may run concurrently (the default strategy is parallel), so this must be a
        // thread-safe collection - List<T>.Add from several threads can lose an entry or tear
        // the backing array.
        ConcurrentBag<Exception> exceptions = [];

        // Local function to process individual handler invocation
        async Task ProcessHandler(object handler, CancellationToken ct)
        {
            if (handler == null) return;
            try
            {
                // Get the Handle(TNotification, CancellationToken) method for this handler type, using cache.
                var handlerMethod = s_handlerMethodCache.GetOrAdd((handler.GetType(), notificationType), static key =>
                    key.HandlerType.GetMethod("Handle", [key.NotificationType, typeof(CancellationToken)])
                    ?? throw new InvalidOperationException($"[KyrolusMediator] Could not find Handle({key.NotificationType.Name}, CancellationToken) method on handler type {key.HandlerType.FullName}.")
                );

                // Invoke the Handle method using reflection. Result must be a Task.
                var task = (Task?)handlerMethod.Invoke(handler, [notification, ct]);
                if (task != null)
                    await task.ConfigureAwait(false);
                else
                    // This should technically not happen if the handler implements the interface correctly.
                    exceptions.Add(new InvalidOperationException($"[KyrolusMediator] Handler {handler.GetType().FullName} Handle method did not return a Task for notification {notificationType.FullName}."));
            }
            // Catch exceptions thrown directly BY the Handle method implementation
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                exceptions.Add(ex.InnerException);
            }
            // Catch any other exception during method lookup or invocation
            catch (Exception ex)
            {
                exceptions.Add(new InvalidOperationException($"[KyrolusMediator] Error invoking handler {handler.GetType().FullName} for notification {notificationType.FullName}.", ex));
            }
        }

        // Process handlers using the configured strategy
        var handlerDelegates = handlers
            .Where(handler => handler != null)
            .Select(handler => (Func<CancellationToken, Task>)(ct => ProcessHandler(handler!, ct)));
        await effectiveStrategy.PublishAsync(handlerDelegates, cancellationToken).ConfigureAwait(false);

        // If any exceptions were collected during the process, throw them all together.
        if (!exceptions.IsEmpty)
            throw new AggregateException($"[KyrolusMediator] One or more errors occurred while publishing notification '{notificationType.Name}'", exceptions);
    }
}
