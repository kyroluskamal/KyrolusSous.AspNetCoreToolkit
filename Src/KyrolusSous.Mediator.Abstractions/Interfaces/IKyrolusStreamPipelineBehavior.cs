namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>(CancellationToken cancellationToken);

public interface IKyrolusStreamPipelineBehavior<in TRequest, TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
