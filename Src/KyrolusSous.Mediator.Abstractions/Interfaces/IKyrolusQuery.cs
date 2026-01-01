namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusQueryBase
{
}

public interface IKyrolusQuery<out TResponse> : IKyrolusRequest<TResponse>, IKyrolusQueryBase
{

}
