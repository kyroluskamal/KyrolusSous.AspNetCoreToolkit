namespace KyrolusSous.SourceMediator.Implementations;

/// <summary>
/// Concrete implementation of <see cref="ISourcePublisher"/>.
/// Resolves and invokes all registered notification handlers for a given notification,
/// handling exceptions from individual handlers gracefully and ensuring all handlers are attempted.
/// Uses reflection with caching for invocation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SourcePublisher"/> class.
/// </remarks>
/// <param name="serviceProvider">The service provider instance used to resolve notification handlers.</param>
/// <exception cref="ArgumentNullException">Thrown if serviceProvider is null.</exception>
public sealed class SourcePublisher(IServiceProvider serviceProvider) : ISourcePublisher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    // Cache MethodInfo for Handle methods per handler type to improve reflection performance.
    // Key: Concrete handler implementation type (e.g., typeof(MyNotificationHandler))
    // Value: MethodInfo for its Handle(TNotification, CancellationToken) method
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_handlerMethodCache = new();

    /// <inheritdoc />
    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var notificationType = notification.GetType();
        var handlerTypeDefinition = typeof(INotificationHandler<>);
        var handlerInterfaceType = handlerTypeDefinition.MakeGenericType(notificationType);

        // Resolve ALL registered handlers for this specific notification type from DI
        var handlers = _serviceProvider.GetServices(handlerInterfaceType).ToList();

        if (handlers.Count == 0) return;

        List<Exception> exceptions = [];

        // Local function to process individual handler invocation
        async Task ProcessHandler(object handler)
        {
            if (handler == null) return;
            try
            {
                // Get the Handle(TNotification, CancellationToken) method for this handler type, using cache.
                var handlerMethod = s_handlerMethodCache.GetOrAdd(handler.GetType(), type =>
                    type.GetMethod("Handle", [notificationType, typeof(CancellationToken)])
                    ?? throw new InvalidOperationException($"[SourceMediator] Could not find Handle({notificationType.Name}, CancellationToken) method on handler type {type.FullName}.")
                );

                // Invoke the Handle method using reflection. Result must be a Task.
                var task = (Task?)handlerMethod.Invoke(handler, new object[] { notification, cancellationToken });
                if (task != null)
                {
                    await task.ConfigureAwait(false);
                }
                else
                {
                    // This should technically not happen if the handler implements the interface correctly.
                    exceptions.Add(new InvalidOperationException($"[SourceMediator] Handler {handler.GetType().FullName} Handle method did not return a Task for notification {notificationType.FullName}."));
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
                exceptions.Add(new InvalidOperationException($"[SourceMediator] Error invoking handler {handler.GetType().FullName} for notification {notificationType.FullName}.", ex));
            }
        }

        // Process all handlers concurrently
        var processingTasks = handlers.Where(handler => handler != null).Select(handler => ProcessHandler(handler!));
        await Task.WhenAll(processingTasks).ConfigureAwait(false);

        // If any exceptions were collected during the process, throw them all together.
        if (exceptions.Count > 0)
        {
            throw new AggregateException($"[SourceMediator] One or more errors occurred while publishing notification '{notificationType.Name}'", exceptions);
        }
    }
}