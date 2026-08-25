namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Finds the handler for a message and invokes it. This is the one job the mediator delegates
/// away, which is what lets the same pipeline run over completely different lookup strategies.
/// </summary>
/// <remarks>
/// Two implementations ship with the toolkit:
/// <list type="bullet">
/// <item><description>
/// The dispatcher emitted by <c>KyrolusSous.Mediator.Generator</c> at build time. It holds a
/// dictionary from request type to a directly-invoked handler call, so dispatch costs a lookup
/// and a delegate call, and every handler name is visible to the trimmer - which is what makes
/// Native AOT viable.
/// </description></item>
/// <item><description>
/// <c>KyrolusReflectionDispatcher</c>, the fallback when the generator is not referenced. It
/// resolves the handler interface and its <c>Handle</c> method reflectively on each dispatch.
/// Slower, and not trim-safe, but it works with no build-time step.
/// </description></item>
/// </list>
/// Registration uses <c>TryAdd</c>, so whichever is registered first wins: the generated one
/// registers itself, and the reflection one only fills in when nothing else did.
/// <para>
/// The interface is public because the generated implementation lives in the consumer's own
/// assembly and has to be able to name it.
/// </para>
/// </remarks>
public interface IKyrolusMediatorDispatcher
{
    /// <summary>
    /// Dispatches a request that expects a response (queries, and commands returning a value).
    /// </summary>
    /// <param name="request">The message. Boxed, because the concrete type varies per call.</param>
    /// <param name="sp">
    /// Used to resolve the handler. Passed in rather than captured so a singleton dispatcher can
    /// still resolve scoped handlers from the caller's scope.
    /// </param>
    /// <param name="ct">Cancellation token forwarded to the handler.</param>
    Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct);

    /// <summary>
    /// Dispatches a command that produces no value (its handler returns a bare <see cref="Task"/>).
    /// </summary>
    Task DispatchCommandAsync(object command, IServiceProvider sp, CancellationToken ct);

    /// <summary>
    /// Dispatches a streaming request, whose handler yields items as they become available.
    /// </summary>
    IAsyncEnumerable<TResponse> DispatchStreamAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct);
}
