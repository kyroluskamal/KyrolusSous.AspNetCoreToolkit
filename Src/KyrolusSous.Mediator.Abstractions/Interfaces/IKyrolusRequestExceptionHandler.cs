namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Recovers from an exception by supplying a replacement response. Calling
/// <see cref="KyrolusRequestExceptionHandlerState{TResponse}.SetHandled"/> cancels the exception
/// and returns that value to the caller instead.
/// </summary>
/// <remarks>
/// Think of it as a spare tyre: it keeps the request moving. To merely record the failure and let
/// it propagate, use <see cref="IKyrolusRequestExceptionAction{TRequest, TException}"/>.
/// <para>
/// Handlers run <b>after</b> all exception actions, and stop at the first one that recovers -
/// there can only be one response, so a second handler would have nothing to contribute. If no
/// handler calls <c>SetHandled</c>, the original exception is rethrown untouched.
/// </para>
/// <para>
/// Recover only when you genuinely have a valid answer. Leaving the state alone is the correct
/// outcome when you do not - see the example, where a cache miss deliberately lets the exception
/// through rather than inventing a number.
/// </para>
/// <para>
/// Matching walks the exception's inheritance chain from most specific outwards, so a handler for
/// a precise exception type runs before a general one registered for <see cref="Exception"/>.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type. Contravariant (<c>in</c>): consumed, never returned.</typeparam>
/// <typeparam name="TException">The exception type to recover from.</typeparam>
/// <typeparam name="TResponse">
/// The response type of the request. Must match, since this is what gets returned in the
/// exception's place.
/// </typeparam>
/// <example>
/// <code>
/// public class UseCachedRateWhenApiIsDown(IMemoryCache cache)
///     : IKyrolusRequestExceptionHandler&lt;GetExchangeRate, HttpRequestException, decimal&gt;
/// {
///     public Task Handle(
///         GetExchangeRate request,
///         HttpRequestException exception,
///         KyrolusRequestExceptionHandlerState&lt;decimal&gt; state,
///         CancellationToken cancellationToken)
///     {
///         if (cache.TryGetValue&lt;decimal&gt;($"rate:{request.From}:{request.To}", out var lastKnown))
///         {
///             state.SetHandled(lastKnown);   // a stale rate beats an error page
///         }
///
///         // Nothing cached: leave the state untouched so the exception still surfaces.
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusRequestExceptionHandler<in TRequest, in TException, TResponse>
    where TException : Exception
{
    /// <summary>Attempts to recover from the exception.</summary>
    /// <param name="request">The request that failed.</param>
    /// <param name="exception">The exception the handler or a behavior threw.</param>
    /// <param name="state">
    /// Call <see cref="KyrolusRequestExceptionHandlerState{TResponse}.SetHandled"/> on it to
    /// cancel the exception and return a value. Leave it alone to let the exception through.
    /// </param>
    /// <param name="cancellationToken">Signals that the caller gave up.</param>
    Task Handle(TRequest request,
        TException exception,
        KyrolusRequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken);
}
