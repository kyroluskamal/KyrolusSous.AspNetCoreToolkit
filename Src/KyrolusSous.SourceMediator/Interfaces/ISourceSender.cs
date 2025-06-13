namespace KyrolusSous.SourceMediator.Interfaces;

/// <summary>
/// Defines a mechanism for sending requests (Queries and Commands) through the mediator pipeline.
/// </summary>
public interface ISourceSender
{
    /// <summary>
    /// Asynchronously sends a query to be handled by a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the query.</typeparam>
    /// <param name="query">The query object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the handler's response.</returns>
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously sends a command to be handled by a single handler.
    /// Use this overload for commands that do not return a value (handler returns Task).
    /// </summary>
    /// <param name="command">The command object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously sends a command that returns a value to be handled by a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the command.</typeparam>
    /// <param name="command">The command object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the handler's response.</returns>
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);
}