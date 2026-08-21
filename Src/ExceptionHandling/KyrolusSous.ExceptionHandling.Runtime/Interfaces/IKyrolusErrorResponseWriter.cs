namespace KyrolusSous.ExceptionHandling.Runtime.Interfaces;

public interface IKyrolusErrorResponseWriter
{
    Task WriteAsync(HttpContext context, KyrolusExceptionMapping mapping, KyrolusErrorContext errorContext, CancellationToken cancellationToken);
}
