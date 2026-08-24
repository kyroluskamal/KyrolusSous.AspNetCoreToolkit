namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// A stream behavior that does nothing but hand the stream straight through.
/// </summary>
[PipelineOrder(0)]
public sealed class KyrolusStreamPassThroughBehavior<TRequest, TResponse>
    : IKyrolusStreamPipelineBehavior<TRequest, TResponse>
{
    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> Handle(TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        return next(cancellationToken);
    }
}
