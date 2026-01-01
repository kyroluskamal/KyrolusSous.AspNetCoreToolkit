namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusRequestPreProcessor<in TRequest>
{
    Task Process(TRequest request, CancellationToken cancellationToken);
}
