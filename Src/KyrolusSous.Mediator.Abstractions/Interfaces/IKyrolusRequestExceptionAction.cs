namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusRequestExceptionAction<in TRequest, in TException>
    where TException : Exception
{
    Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
