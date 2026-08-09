namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

public interface IKyrolusExceptionMapper
{
    int Order { get; }

    bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping);
}
