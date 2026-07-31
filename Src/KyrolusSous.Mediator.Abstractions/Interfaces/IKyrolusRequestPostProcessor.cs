namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Runs after the handler has produced a response, and can see both the request and the response.
/// </summary>
/// <remarks>
/// <c>Process</c> returns a bare <see cref="Task"/>, not the response - so a post-processor can
/// <em>read</em> the response but not replace it. That is deliberate: if any post-processor could
/// swap the response, tracing which one did would be guesswork. To change a response, use
/// <see cref="IKyrolusPipelineBehavior{TRequest, TResponse}"/> instead.
/// <para>
/// It only runs when the handler succeeded. If the handler threw, there is no response, so no
/// post-processor runs - use <see cref="IKyrolusRequestExceptionAction{TRequest, TException}"/>
/// for work that must happen on failure too.
/// </para>
/// <para>
/// Collected by <c>KyrolusRequestPostProcessorBehavior</c> at order +1000, making it the innermost
/// layer - it sees the response before any of your own behaviors do.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type. Contravariant (<c>in</c>): consumed, never returned.</typeparam>
/// <typeparam name="TResponse">The response type. Contravariant (<c>in</c>): read, never returned.</typeparam>
/// <example>
/// <code>
/// public class AuditUserLookups(IAuditLog audit) : IKyrolusRequestPostProcessor&lt;GetUser, User?&gt;
/// {
///     public Task Process(GetUser request, User? response, CancellationToken cancellationToken)
///         =&gt; audit.WriteAsync(
///             response is null ? $"miss:{request.Id}" : $"hit:{request.Id}",
///             cancellationToken);
/// }
/// </code>
/// </example>
public interface IKyrolusRequestPostProcessor<in TRequest, in TResponse>
{
    /// <summary>Runs after the handler returned successfully.</summary>
    /// <param name="request">The request that was handled.</param>
    /// <param name="response">What the handler produced. Read-only - returning a new value is not possible here.</param>
    /// <param name="cancellationToken">Signals that the caller gave up.</param>
    /// <returns>A task that completes when this post-processor is done.</returns>
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
