namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// The next step in a stream pipeline - either the following behavior or the handler itself.
/// Calling it returns the stream; not calling it replaces the stream entirely.
/// </summary>
/// <remarks>
/// Unlike <see cref="RequestHandlerDelegate{TResponse}"/>, the token has no default value: a
/// stream lives longer than a single call, so passing cancellation through it is not optional.
/// </remarks>
/// <typeparam name="TResponse">The type of a single streamed item.</typeparam>
/// <param name="cancellationToken">Token forwarded to the rest of the pipeline.</param>
/// <returns>The stream produced by the remaining pipeline.</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>(CancellationToken cancellationToken);

/// <summary>
/// Wraps a streaming request the way <see cref="IKyrolusPipelineBehavior{TRequest, TResponse}"/>
/// wraps an ordinary one - logging, throttling, filtering items as they pass.
/// </summary>
/// <remarks>
/// One difference matters in practice. An ordinary behavior can measure a request by timing
/// <c>await next()</c>, because that call is the whole operation. Here, <c>next(ct)</c> returns
/// immediately with a stream that has produced nothing yet; the work happens later, while the
/// caller enumerates. To observe items you must enumerate the inner stream yourself and
/// <c>yield return</c> each one onwards - see the example.
/// <para>
/// Ordering follows the same rules as ordinary behaviors: <see cref="Attributes.PipelineOrderAttribute"/>
/// first, then registration order for anything sharing the same value.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The stream request type. Contravariant (<c>in</c>): consumed, never returned.</typeparam>
/// <typeparam name="TResponse">The type of a single streamed item.</typeparam>
/// <example>
/// <code>
/// public class CountStreamedItems&lt;TRequest, TResponse&gt;(IMetrics metrics)
///     : IKyrolusStreamPipelineBehavior&lt;TRequest, TResponse&gt;
/// {
///     public async IAsyncEnumerable&lt;TResponse&gt; Handle(
///         TRequest request,
///         StreamHandlerDelegate&lt;TResponse&gt; next,
///         [EnumeratorCancellation] CancellationToken cancellationToken)
///     {
///         var count = 0;
///
///         await foreach (var item in next(cancellationToken).WithCancellation(cancellationToken))
///         {
///             count++;
///             yield return item;      // pass it on untouched
///         }
///
///         metrics.Record("streamed_items", count);   // runs once the stream is exhausted
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusStreamPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>Wraps the stream.</summary>
    /// <param name="request">The stream request.</param>
    /// <param name="next">
    /// The rest of the pipeline. Returns immediately - the items are produced as they are
    /// enumerated, not when this is called.
    /// </param>
    /// <param name="cancellationToken">Token to forward to <paramref name="next"/>.</param>
    /// <returns>The stream to hand to the caller: usually <paramref name="next"/>'s, possibly wrapped.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
