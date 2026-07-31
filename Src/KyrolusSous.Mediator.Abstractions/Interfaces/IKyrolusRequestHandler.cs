namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Handles a request and produces a response. Exactly one handler may exist per request type -
/// registering a second one is reported at startup rather than silently ignored.
/// </summary>
/// <remarks>
/// This is the root handler contract. Prefer <see cref="IKyrolusQueryHandler{TQuery, TResponse}"/>
/// or <see cref="IKyrolusCommandHandler{TCommand, TResponse}"/>, which say whether the operation
/// reads or writes. Implement this one directly only for a request that is genuinely neither.
/// <para>
/// Handlers are resolved from dependency injection per request, so constructor dependencies such
/// as a <c>DbContext</c> behave exactly as they would in a controller.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">
/// The request type. Contravariant (<c>in</c>): the handler consumes the request, never returns it.
/// </typeparam>
/// <typeparam name="TResponse">The type the handler produces.</typeparam>
/// <example>
/// <code>
/// public record GetUser(int Id) : IKyrolusRequest&lt;User&gt;;
///
/// public class GetUserHandler(AppDbContext db) : IKyrolusRequestHandler&lt;GetUser, User&gt;
/// {
///     public async Task&lt;User&gt; Handle(GetUser request, CancellationToken cancellationToken)
///         =&gt; await db.Users.FindAsync([request.Id], cancellationToken);
/// }
/// </code>
/// </example>
public interface IKyrolusRequestHandler<in TRequest, TResponse>
    where TRequest : IKyrolusRequest<TResponse>
{
    /// <summary>
    /// Executes the request. Called by the mediator after every pipeline behavior has run, so a
    /// request that reaches here has already passed validation and any other configured checks.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">
    /// Signals that the caller gave up - a cancelled HTTP request, for example. Pass it on to
    /// database and network calls; nothing cancels a handler on its behalf.
    /// </param>
    /// <returns>The response. Returning <see langword="null"/> for a non-nullable
    /// <typeparamref name="TResponse"/> is reported as an <see cref="InvalidCastException"/>.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a request that produces no value.
/// </summary>
/// <remarks>
/// The mediator represents "no value" as <see cref="Unit"/> internally, but this overload lets a
/// handler return a plain <see cref="Task"/> so <see cref="Unit"/> never appears in your code.
/// </remarks>
/// <typeparam name="TRequest">The request type, constrained to one whose response is <see cref="Unit"/>.</typeparam>
public interface IKyrolusRequestHandler<in TRequest>
    where TRequest : IKyrolusRequest<Unit>
{
    /// <summary>
    /// Executes the request. Called by the mediator after every pipeline behavior has run.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">Signals that the caller gave up. Pass it on to database and network calls.</param>
    /// <returns>A task that completes when the work is done.</returns>
    Task Handle(TRequest request, CancellationToken cancellationToken);
}
