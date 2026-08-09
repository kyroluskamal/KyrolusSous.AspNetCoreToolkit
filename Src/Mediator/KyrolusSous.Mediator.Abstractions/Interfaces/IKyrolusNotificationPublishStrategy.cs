namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Decides <em>how</em> notification handlers are run - all at once, or one after another.
/// </summary>
/// <remarks>
/// It does not decide <em>which</em> handlers run. By the time a strategy is called the publisher
/// has already resolved them from dependency injection and wrapped each one in a delegate; they
/// arrive as the <c>handlers</c> parameter. The only decision left is scheduling.
/// <para>
/// Two implementations ship with the runtime. <c>KyrolusParallelNotificationPublishStrategy</c>
/// starts every handler and awaits them together - the default, and the faster one.
/// <c>KyrolusSequentialNotificationPublishStrategy</c> awaits each before starting the next.
/// </para>
/// <para>
/// <b>Choose sequential when handlers share something that is not thread-safe.</b> The usual case
/// is a <c>DbContext</c>: it is scoped to the request, so parallel handlers touching it hit
/// "a second operation was started on this context instance". Switch with
/// <c>NotificationPublishMode.Sequential</c> at startup, or pass a strategy to a single
/// <c>PublishAsync</c> call to override it just there.
/// </para>
/// <para>
/// Implement this yourself for anything else - bounded concurrency, per-handler timeouts,
/// ordering by priority.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Runs handlers in parallel, but never more than three at a time.
/// public class BoundedPublishStrategy : IKyrolusNotificationPublishStrategy
/// {
///     public async Task PublishAsync(
///         IEnumerable&lt;Func&lt;CancellationToken, Task&gt;&gt; handlers,
///         CancellationToken cancellationToken)
///     {
///         using var gate = new SemaphoreSlim(3);
///
///         await Task.WhenAll(handlers.Select(async handler =&gt;
///         {
///             await gate.WaitAsync(cancellationToken);
///             try { await handler(cancellationToken); }
///             finally { gate.Release(); }
///         }));
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusNotificationPublishStrategy
{
    /// <summary>Runs the handlers.</summary>
    /// <param name="handlers">
    /// One delegate per registered handler, already resolved and ready to invoke. Each one
    /// captures its own exceptions, so a handler that throws will not stop the others - do not
    /// add your own error handling around them.
    /// </param>
    /// <param name="cancellationToken">Token to pass to each handler.</param>
    /// <returns>A task that completes once every handler has finished.</returns>
    Task PublishAsync(IEnumerable<Func<CancellationToken, Task>> handlers, CancellationToken cancellationToken);
}
