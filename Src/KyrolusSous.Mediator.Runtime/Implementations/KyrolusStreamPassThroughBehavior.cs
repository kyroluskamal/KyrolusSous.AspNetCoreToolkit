namespace KyrolusSous.Mediator.Runtime.Implementations;

[PipelineOrder(0)]
public sealed class KyrolusStreamPassThroughBehavior<TRequest, TResponse>
    : IKyrolusStreamPipelineBehavior<TRequest, TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return next(cancellationToken);
    }
}
