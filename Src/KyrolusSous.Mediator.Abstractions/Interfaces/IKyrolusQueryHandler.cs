namespace KyrolusSous.Mediator.Abstractions.Interfaces;
public interface IKyrolusQueryHandler<in TQuery, TResponse> : IKyrolusRequestHandler<TQuery, TResponse>
    where TQuery : IKyrolusQuery<TResponse>
{
}
