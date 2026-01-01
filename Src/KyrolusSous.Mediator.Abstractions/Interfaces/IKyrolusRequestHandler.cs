namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusRequestHandler<in TRequest, TResponse>
    where TRequest : IKyrolusRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IKyrolusRequestHandler<in TRequest>
    where TRequest : IKyrolusRequest<Unit>
{
    Task Handle(TRequest request, CancellationToken cancellationToken);
}
