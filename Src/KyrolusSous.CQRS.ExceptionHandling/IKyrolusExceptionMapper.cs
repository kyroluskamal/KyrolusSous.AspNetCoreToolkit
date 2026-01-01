namespace KyrolusSous.CQRS.ExceptionHandling;

public interface IKyrolusExceptionMapper<TResponse>
{
    bool TryMap(Exception exception, out TResponse response);
}
