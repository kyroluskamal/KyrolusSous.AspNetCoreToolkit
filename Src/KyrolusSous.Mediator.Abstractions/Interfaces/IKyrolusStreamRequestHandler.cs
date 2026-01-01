namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IKyrolusStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
