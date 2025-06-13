namespace KyrolusSous.SourceMediator.Interfaces;
/// <summary>
/// Represents a void type, since System.Void cannot be used as a generic type argument.
/// Primarily used for pipeline behaviors handling commands that don't return a value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The single, default value of <see cref="Unit"/>.
    /// </summary>
    public static readonly Unit Value = new();

    /// <inheritdoc/>
    public override int GetHashCode() => 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc/>
    public bool Equals(Unit other) => true; // All Unit values are equal

    /// <summary>
    /// Compares two <see cref="Unit"/> values for equality (always true).
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Compares two <see cref="Unit"/> values for inequality (always false).
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;

    /// <inheritdoc/>
    public override string ToString() => "()";
}

/// <summary>
/// Represents the next action in the mediator pipeline.
/// This could be the next behavior or the actual request handler.
/// </summary>
/// <typeparam name="TResponse">The response type of the request.</typeparam>
/// <returns>A task representing the asynchronous operation, yielding the response.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Defines a pipeline behavior for processing requests.
/// Implementations can add cross-cutting concerns before and after the actual request handler is executed.
/// </summary>
/// <typeparam name="TRequest">The type of the request being handled.</typeparam>
/// <typeparam name="TResponse">
/// The type of the response from the handler. Use <see cref="Unit"/> for handlers that return Task (originally void).
/// </typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the request by potentially performing actions before and after invoking the next delegate in the pipeline.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="next">The delegate representing the next action in the pipeline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, yielding the response.</returns>
    Task<TResponse> Handle(TRequest request,
                         RequestHandlerDelegate<TResponse> next,
                         CancellationToken cancellationToken);
}