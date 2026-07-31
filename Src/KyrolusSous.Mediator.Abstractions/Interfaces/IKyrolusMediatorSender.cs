namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Defines a mechanism for sending requests (Queries and Commands) through the mediator pipeline.
/// </summary>
public interface IKyrolusMediatorSender
{
    /// <summary>
    /// Asynchronously sends a query to be handled by a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the query.</typeparam>
    /// <param name="query">The query object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the handler's response.</returns>
    Task<TResponse> SendAsync<TResponse>(IKyrolusQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously sends a request to be handled by a single handler.
    /// Use this overload to send any request implementing <see cref="IKyrolusRequest{TResponse}"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the request.</typeparam>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the handler's response.</returns>
    Task<TResponse> SendAsync<TResponse>(IKyrolusRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously sends a command to be handled by a single handler.
    /// Use this overload for commands that do not return a value (handler returns Task).
    /// </summary>
    /// <param name="command">The command object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendAsync(IKyrolusCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously sends a command that returns a value to be handled by a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the command.</typeparam>
    /// <param name="command">The command object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the handler's response.</returns>
    Task<TResponse> SendAsync<TResponse>(IKyrolusCommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously creates a stream for a streaming request.
    /// </summary>
    /// <typeparam name="TResponse">The type of streamed items.</typeparam>
    /// <param name="request">The stream request object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>An async stream of responses.</returns>
    IAsyncEnumerable<TResponse> StreamAsync<TResponse>(IKyrolusStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request whose response type is not known at compile time. Use this when the
    /// request arrives as <see cref="object"/> - from deserialization, a queue consumer, or a
    /// generic outbox - and the concrete type only exists at runtime.
    /// </summary>
    /// <param name="request">The request object. Must implement <see cref="IKyrolusRequest{TResponse}"/>.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>The handler's response, boxed.</returns>
    /// <exception cref="ArgumentException">The object does not implement <see cref="IKyrolusRequest{TResponse}"/>.</exception>
    Task<object?> SendAsync(object request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a request whose item type is not known at compile time.
    /// </summary>
    /// <param name="request">The stream request object. Must implement <see cref="IKyrolusStreamRequest{TResponse}"/>.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>An async stream of boxed responses.</returns>
    /// <exception cref="ArgumentException">The object does not implement <see cref="IKyrolusStreamRequest{TResponse}"/>.</exception>
    IAsyncEnumerable<object?> StreamAsync(object request, CancellationToken cancellationToken = default);
}
