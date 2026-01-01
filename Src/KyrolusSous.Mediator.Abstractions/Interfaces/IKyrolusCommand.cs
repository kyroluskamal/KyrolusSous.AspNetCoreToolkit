namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusCommandBase
{
}

public interface IKyrolusCommand : IKyrolusRequest<Unit>, IKyrolusCommandBase
{

}
public interface IKyrolusCommand<out TResponse> : IKyrolusRequest<TResponse>, IKyrolusCommandBase
{
}
