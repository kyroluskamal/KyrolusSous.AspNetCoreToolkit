namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// A stream behavior that does nothing but hand the stream straight through.
/// </summary>
/// <remarks>
/// It looks pointless, and functionally it is - the value is that a stream pipeline is never
/// empty. Registering it means the resolution path for
/// <see cref="IKyrolusStreamPipelineBehavior{TRequest, TResponse}"/> is exercised on every stream
/// request, so a real behavior added later slots into a path that already works rather than one
/// that has never run.
/// <para>
/// Its cost is one delegate call per stream request - not per item - because it returns
/// <c>next(...)</c> without enumerating it.
/// </para>
/// <para>
/// Note what it does <em>not</em> do: it never wraps the stream in <c>await foreach</c>. A
/// behavior that needs to see the items must enumerate the inner stream and <c>yield return</c>
/// each one, which is a different and much more expensive shape - see
/// <see cref="IKyrolusStreamPipelineBehavior{TRequest, TResponse}"/> for an example.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The stream request type.</typeparam>
/// <typeparam name="TResponse">The type of a single streamed item.</typeparam>
[PipelineOrder(0)]
public sealed class KyrolusStreamPassThroughBehavior<TRequest, TResponse>
    : IKyrolusStreamPipelineBehavior<TRequest, TResponse>
{
    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> Handle(TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
        => next(cancellationToken);
}
