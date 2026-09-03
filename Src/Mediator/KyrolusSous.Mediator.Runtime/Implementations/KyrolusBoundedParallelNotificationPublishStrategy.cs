namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Runs notification handlers in parallel, capped at a maximum number running at once.
/// </summary>
/// <remarks>
/// <see cref="KyrolusParallelNotificationPublishStrategy"/> starts every handler at once with no
/// limit at all - fine for a handful of handlers, but a notification that fans out to many of
/// them (or to a few expensive ones) can exhaust a connection pool or the thread pool shared with
/// the rest of the application. This strategy is the same idea with a ceiling: at most
/// <c>maxDegreeOfParallelism</c> handlers run at once, and the next one starts only once a slot
/// frees up.
/// <para>
/// Still parallel, not sequential: handlers are not guaranteed to run in any particular order, and
/// several of them are still running concurrently up to the cap. Use
/// <see cref="KyrolusSequentialNotificationPublishStrategy"/> instead when handlers share a
/// resource - such as a scoped <c>DbContext</c> - that tolerates only one operation at a time.
/// </para>
/// <para>
/// A handler that fails does not stop the ones after it, and does not free its slot early: each
/// delegate already captures its own exception (see <c>KyrolusMediatorPublisher</c>), so a faulted
/// handler still runs to completion as far as this strategy is concerned.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // For the whole application:
/// builder.Services.AddKyrolusMediator(configuration =&gt;
/// {
///     configuration.RegisterServicesFromAssemblyContaining&lt;Program&gt;();
///     configuration.NotificationPublishMode = NotificationPublishMode.BoundedParallel;
///     configuration.NotificationPublishMaxDegreeOfParallelism = 4;
/// });
///
/// // Or just for one publish:
/// await publisher.PublishAsync(
///     new UserCreated(id, email),
///     new KyrolusBoundedParallelNotificationPublishStrategy(4),
///     cancellationToken);
/// </code>
/// </example>
public sealed class KyrolusBoundedParallelNotificationPublishStrategy : IKyrolusNotificationPublishStrategy
{
    private readonly int _maxDegreeOfParallelism;

    /// <param name="maxDegreeOfParallelism">The most handlers allowed to run at once. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is less than 1.</exception>
    public KyrolusBoundedParallelNotificationPublishStrategy(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maxDegreeOfParallelism),
                maxDegreeOfParallelism,
                "[KyrolusMediator] At least one handler must be allowed to run at a time.");

        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    /// <inheritdoc />
    public async Task PublishAsync(IEnumerable<Func<CancellationToken, Task>> handlers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        using var throttle = new SemaphoreSlim(_maxDegreeOfParallelism, _maxDegreeOfParallelism);
        var running = new List<Task>();

        // Starting a handler costs a slot immediately, before it is awaited - the same "everyone
        // starts as soon as it can" spirit as the unbounded strategy, just capped.
        foreach (var handler in handlers)
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            running.Add(RunAsync(handler, throttle, cancellationToken));
        }

        await Task.WhenAll(running).ConfigureAwait(false);
    }

    private static async Task RunAsync(Func<CancellationToken, Task> handler, SemaphoreSlim throttle, CancellationToken cancellationToken)
    {
        try
        {
            await handler(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            throttle.Release();
        }
    }
}
