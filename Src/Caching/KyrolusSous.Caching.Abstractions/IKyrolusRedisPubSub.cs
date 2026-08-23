namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Provides type-safe publish-subscribe messaging over Redis.
/// </summary>
public interface IKyrolusRedisPubSub
{
    /// <summary>
    /// Publishes a strongly typed message to the specified channel.
    /// </summary>
    /// <typeparam name="T">The type of the message payload.</typeparam>
    /// <param name="channel">The channel name to publish to.</param>
    /// <param name="message">The message payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishAsync<T>(string channel, T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to a channel and executes the handler when a typed message arrives.
    /// Disposing the returned handle unsubscribes from the channel.
    /// </summary>
    /// <typeparam name="T">The type of the message payload.</typeparam>
    /// <param name="channel">The channel name to subscribe to.</param>
    /// <param name="handler">The asynchronous callback to execute when a message arrives.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An asynchronous disposable handle to unsubscribe.</returns>
    Task<IAsyncDisposable> SubscribeAsync<T>(string channel, Func<T, Task> handler, CancellationToken cancellationToken = default);
}
