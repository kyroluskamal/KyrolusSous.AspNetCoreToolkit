namespace KyrolusSous.Mediator.Abstractions.Interfaces;
public interface IKyrolusCommandHandler<in TCommand, TResponse> : IKyrolusRequestHandler<TCommand, TResponse>
    where TCommand : IKyrolusCommand<TResponse>
{
}

public interface IKyrolusCommandHandler<in TCommand> : IKyrolusRequestHandler<TCommand>
    where TCommand : IKyrolusCommand
{
}
