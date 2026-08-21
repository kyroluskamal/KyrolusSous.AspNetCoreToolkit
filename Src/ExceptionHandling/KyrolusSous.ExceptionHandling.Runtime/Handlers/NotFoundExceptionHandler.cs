namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entityName, string key) : base($"{entityName} with key {key} not found") { }
}

public class NotFoundExceptionHandler(ILogger<NotFoundExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is NotFoundException notfound)
        {
            await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
                logger, httpContext, HttpStatusCode.NotFound, KyrolusErrorCodes.NotFound,
                "Not found", notfound.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        return false;
    }
}
