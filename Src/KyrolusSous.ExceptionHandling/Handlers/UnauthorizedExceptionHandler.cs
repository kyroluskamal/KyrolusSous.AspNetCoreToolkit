namespace KyrolusSous.ExceptionHandling.Handlers;
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string entityName, string key) : base($"{entityName} with key {key} not found")
    {
    }
}

public class UnauthorizedExceptionHandler(ILogger<SocketExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var contextInfo = new ErrorContextInfo(httpContext);
        if (exception is UnauthorizedException UnauthorizedException)
        {
            ExceptionHelper.ReturnErrorResponse(logger, httpContext, contextInfo, UnauthorizedException, HttpStatusCode.Unauthorized, UnauthorizedException.Message).GetAwaiter().GetResult();
            return new ValueTask<bool>(true);
        }
        return new ValueTask<bool>(false);
    }
}
