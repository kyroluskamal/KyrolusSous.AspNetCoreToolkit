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
    // Cache MethodInfo for Handle methods per handler type to improve reflection performance.
    // Key: Concrete handler implementation type (e.g., typeof(MyNotificationHandler))
    // Value: MethodInfo for its Handle(TNotification, CancellationToken) method
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_handlerMethodCache = new();

    /// <inheritdoc />
    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        await PublishAsync(notification, null, cancellationToken).ConfigureAwait(false);
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

        List<Exception> exceptions = [];

        // Local function to process individual handler invocation
        async Task ProcessHandler(object handler, CancellationToken ct)
        {
            if (handler == null) return;
            try
            {
                // Get the Handle(TNotification, CancellationToken) method for this handler type, using cache.
                var handlerMethod = s_handlerMethodCache.GetOrAdd(handler.GetType(), type =>
                    type.GetMethod("Handle", [notificationType, typeof(CancellationToken)])
                    ?? throw new InvalidOperationException($"[KyrolusMediator] Could not find Handle({notificationType.Name}, CancellationToken) method on handler type {type.FullName}.")
                );

                // Invoke the Handle method using reflection. Result must be a Task.
                var task = (Task?)handlerMethod.Invoke(handler, new object[] { notification, ct });
                if (task != null)
                {
                    await task.ConfigureAwait(false);
                }
                else
                {
                    // This should technically not happen if the handler implements the interface correctly.
                    exceptions.Add(new InvalidOperationException($"[KyrolusMediator] Handler {handler.GetType().FullName} Handle method did not return a Task for notification {notificationType.FullName}."));
                }
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
        if (exceptions.Count > 0)
        {
            throw new AggregateException($"[KyrolusMediator] One or more errors occurred while publishing notification '{notificationType.Name}'", exceptions);
        }
    }
}
