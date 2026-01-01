namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusRequestExceptionHandler<in TRequest, in TException, TResponse>
    where TException : Exception
{
    Task Handle(TRequest request,
        TException exception,
        KyrolusRequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken);
}
