namespace KyrolusSous.RabbitMQ.Abstractions.Interfaces;

/// <summary>
/// RPC client for request-reply messaging over RabbitMQ.
/// </summary>
public interface IKyrolusRabbitMQRpcClient
{
    /// <summary>
    /// Sends a request message and asynchronously waits for a matching response using Direct-Reply-To.
    /// </summary>
    /// <typeparam name="TRequest">Request payload type.</typeparam>
    /// <typeparam name="TResponse">Expected response payload type.</typeparam>
    /// <param name="exchange">Target exchange.</param>
    /// <param name="routingKey">Routing key.</param>
    /// <param name="request">Request object.</param>
    /// <param name="timeout">Optional timeout duration (default: 30 seconds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized response object.</returns>
    Task<TResponse> RequestAsync<TRequest, TResponse>(
        string exchange,
        string routingKey,
        TRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
