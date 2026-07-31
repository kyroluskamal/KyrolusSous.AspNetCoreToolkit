namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Runs notification handlers one after another, each finishing before the next starts.
/// </summary>
/// <remarks>
/// Slower than <see cref="KyrolusParallelNotificationPublishStrategy"/> - total time is the sum of
/// all handlers - but safe when they share a resource that only tolerates one operation at a time.
/// <para>
/// The usual reason to pick it is <c>DbContext</c>. It is registered scoped, so every handler in a
/// request gets the same instance, and it does not support concurrent operations. Running such
/// handlers in parallel produces "A second operation was started on this context instance".
/// </para>
/// <para>
/// A side effect worth knowing: handlers run in DI registration order and that order is
/// observable. Do not build on it - notification handlers are meant to be independent, and code
/// that quietly relies on one running before another will break the day someone reorders a
/// registration.
/// </para>
/// <para>
/// A handler that fails does not stop the ones after it. Each delegate captures its own exception,
/// and the publisher raises them together at the end.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // For the whole application:
/// builder.Services.AddKyrolusMediator(configuration =&gt;
/// {
///     configuration.RegisterServicesFromAssemblyContaining&lt;Program&gt;();
///     configuration.NotificationPublishMode = NotificationPublishMode.Sequential;
/// });
///
/// // Or just for one publish that touches the database:
/// await publisher.PublishAsync(
///     new UserCreated(id, email),
///     new KyrolusSequentialNotificationPublishStrategy(),
///     cancellationToken);
/// </code>
/// </example>
public sealed class KyrolusSequentialNotificationPublishStrategy : IKyrolusNotificationPublishStrategy
{
    /// <inheritdoc />
    public async Task PublishAsync(IEnumerable<Func<CancellationToken, Task>> handlers, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
            await handler(cancellationToken).ConfigureAwait(false);
    }
}
