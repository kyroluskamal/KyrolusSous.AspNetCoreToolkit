namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusRequestPostProcessor<in TRequest, in TResponse>
{
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
