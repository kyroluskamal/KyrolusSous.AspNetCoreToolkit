namespace KyrolusSous.ExceptionHandling.Handlers;

public class SocketExceptionHandler(ILogger<SocketExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var contextInfo = new ErrorContextInfo(httpContext);
        if (exception is SocketException validationException)
        {
            ExceptionHelper.ReturnErrorResponse(logger, httpContext, contextInfo, validationException, HttpStatusCode.InternalServerError, "Socket is not found").GetAwaiter().GetResult();
            return new ValueTask<bool>(true);
        }
        return new ValueTask<bool>(false);
    }
}