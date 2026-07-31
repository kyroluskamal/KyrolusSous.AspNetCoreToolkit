namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Starts every notification handler at once and waits for all of them. The default strategy.
/// </summary>
/// <remarks>
/// Total time is the slowest handler rather than the sum of all of them, which is why it is the
/// default: notification handlers are independent by definition, so there is usually no reason to
/// make them queue.
/// <para>
/// <b>Do not use it when handlers share something that is not thread-safe.</b> The usual case is
/// a <c>DbContext</c>: it is scoped to the request, so two handlers touching it together produce
/// "A second operation was started on this context instance". Switch to
/// <see cref="KyrolusSequentialNotificationPublishStrategy"/> there.
/// </para>
/// <para>
/// Nothing bounds the concurrency. Fifty handlers all start at once. Implement
/// <see cref="IKyrolusNotificationPublishStrategy"/> yourself if you need a limit.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The default - nothing to configure.
/// builder.Services.AddKyrolusMediatorFromAssemblies(typeof(Program).Assembly);
///
/// // Or for one call only:
/// await publisher.PublishAsync(
///     new UserCreated(id, email),
///     new KyrolusParallelNotificationPublishStrategy(),
///     cancellationToken);
/// </code>
/// </example>
public sealed class KyrolusParallelNotificationPublishStrategy : IKyrolusNotificationPublishStrategy
{
    /// <inheritdoc />
    /// <remarks>
    /// Calling each delegate starts its work immediately; <c>Select</c> is enumerated by
    /// <see cref="Task.WhenAll(System.Collections.Generic.IEnumerable{Task})"/>, so all of them are
    /// running before the first is awaited.
    /// <para>
    /// No try/catch here: each delegate already captures its own exceptions, and the publisher
    /// collects them into one <see cref="AggregateException"/> afterwards.
    /// </para>
    /// </remarks>
    public Task PublishAsync(IEnumerable<Func<CancellationToken, Task>> handlers, CancellationToken cancellationToken)
    {
        var tasks = handlers.Select(handler => handler(cancellationToken));
        return Task.WhenAll(tasks);
    }
}
