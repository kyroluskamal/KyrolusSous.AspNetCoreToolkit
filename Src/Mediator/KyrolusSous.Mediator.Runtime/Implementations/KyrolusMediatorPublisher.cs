using KyrolusSous.Mediator.Runtime.GeneratorIntegration;
using Microsoft.Extensions.Logging;

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
    /// <summary>Stable logger category, rather than the mangled type name of whichever dispatch source is registered.</summary>
    private const string LoggerCategory = "KyrolusSous.Mediator.Publisher";

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
    public async Task PublishAsync(IKyrolusNotification notification, CancellationToken cancellationToken = default)
    => await PublishAsync(notification, null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task PublishAsync(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification is not IKyrolusNotification typed)
            throw new ArgumentException(
                $"[KyrolusMediator] {notification.GetType().FullName} does not implement {nameof(IKyrolusNotification)}.",
                nameof(notification));

        await PublishAsync(typed, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync(IKyrolusNotification notification, IKyrolusNotificationPublishStrategy? strategy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveStrategy = strategy ?? _publishStrategy;
        var notificationType = notification.GetType();

        var source = _dispatchSource ?? throw new InvalidOperationException(
            "[KyrolusMediator] No notification dispatch source is registered. Reference " +
            "KyrolusSous.Mediator.Generator and call AddKyrolusMediatorNotificationHandlers(), or " +
            "reference KyrolusSous.Mediator.Reflection and call AddKyrolusMediatorReflection().");

        // Null means the source has never seen this notification type, which is not the same as
        // having seen it and found no handler. Both end up doing nothing, but only the second is
        // normal, so they are not collapsed into one branch - see LogUnknownNotificationType.
        var invocations = source.CreateHandlerInvocations(notification, _serviceProvider);
        if (invocations is null)
        {
            LogUnknownNotificationType(notificationType, source);
            return;
        }

        if (invocations.Count == 0) return;

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
        })).ToArray();

        await effectiveStrategy.PublishAsync(guarded, cancellationToken).ConfigureAwait(false);

        // If any exceptions were collected during the process, throw them all together.
        if (!exceptions.IsEmpty)
            throw new AggregateException($"[KyrolusMediator] One or more errors occurred while publishing notification '{notificationType.Name}'", exceptions);
    }

    /// <summary>
    /// Reports the one outcome of <see cref="IKyrolusNotificationDispatchSource.CreateHandlerInvocations"/>
    /// that is NOT a legitimate no-op: <paramref name="dispatchSource"/> has never even heard of
    /// <paramref name="notificationType"/>, as opposed to knowing it and simply having zero handlers
    /// registered for this call right now - a notification with nobody currently subscribed is a
    /// completely ordinary, silent case.
    /// </summary>
    /// <remarks>
    /// Only <see cref="IKyrolusNotificationDispatchSource.CreateHandlerInvocations"/> can tell these
    /// two outcomes apart - <see langword="null"/> for the first, an empty list for the second - so
    /// this is the only place able to act on the difference.
    /// <para>
    /// This logs rather than throws, deliberately asymmetric with <see cref="KyrolusMediatorSender"/>'s
    /// analogous "generator never saw this" gap on the request side (its <c>NoWrapper</c> exception).
    /// A request with no handler is always a bug - by definition, something must handle a request - so
    /// throwing there cannot break a legitimate scenario. A notification with no dispatch entry is not
    /// that clear-cut: a handler for it may be entirely real, just declared in an assembly the
    /// generator that built this table never analyzed, or registered only against the MediatR-compat
    /// <c>INotificationHandler&lt;&gt;</c> interface without going through generation (the same
    /// legitimate multi-project split SMG005's own description carves out for requests). Throwing here
    /// would turn a silently-missed subscriber into a hard failure of the ENTIRE publish call, for
    /// every other, unrelated handler of the same notification too.
    /// </para>
    /// <para>
    /// Resolving the handler here instead - the way the reflection package's own
    /// <c>IKyrolusNotificationDispatchSource</c> implementation does, by closing
    /// <c>INotificationHandler&lt;&gt;</c> at runtime - was considered and rejected. This project
    /// (<c>KyrolusSous.Mediator.Runtime</c>) builds with <c>IsAotCompatible</c> and deliberately
    /// contains no reflection-based dispatch, specifically so an application publishing it ahead of
    /// time never needs to reference the reflection package at all - see this class's own remarks and
    /// the comment on the project file. Reproducing that fallback here would undo the entire reason the
    /// two packages are split, and would reintroduce exactly the <c>MakeGenericType</c> use the project
    /// file's comment says the build is set up to catch. Referencing
    /// <c>KyrolusSous.Mediator.Reflection</c> and calling <c>AddKyrolusMediatorReflection()</c> - which
    /// resolves a handler like this correctly - is the documented way out, named in the warning itself.
    /// </para>
    /// </remarks>
    private void LogUnknownNotificationType(Type notificationType, IKyrolusNotificationDispatchSource dispatchSource)
    {
        try
        {
            _serviceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger(LoggerCategory)
                .LogWarning(
                    "[KyrolusMediator] Publishing '{NotificationType}' found no dispatch entry for it at all: " +
                    "{DispatchSourceType} has never seen a handler for this notification type, which is different " +
                    "from \"it has seen it, and zero handlers happen to be registered right now\" (an ordinary, " +
                    "silent no-op). This usually means a handler for it exists in an assembly the source generator " +
                    "did not analyze, or is registered only against the MediatR-compat INotificationHandler<> " +
                    "interface without being generated for - either way, it will NOT run from this call. Reference " +
                    "KyrolusSous.Mediator.Reflection and call AddKyrolusMediatorReflection() to resolve handlers " +
                    "like that at runtime instead.",
                    notificationType.FullName,
                    dispatchSource.GetType().Name);
        }
        catch
        {
            // Logging itself failing must not turn a warning about a missed handler into a hard
            // failure of an otherwise-successful publish call.
        }
    }
}
