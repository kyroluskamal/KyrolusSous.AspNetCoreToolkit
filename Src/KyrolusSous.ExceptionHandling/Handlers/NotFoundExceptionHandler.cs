namespace KyrolusSous.ExceptionHandling.Handlers;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, string key) : base($"{entityName} with key {key} not found")
    {
    }
}

public class NotFoundExceptionHandler(ILogger<NotFoundExceptionHandler> logger) : IExceptionHandler
{


    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var contextInfo = new ErrorContextInfo(httpContext);
        if (exception is NotFoundException notfound)
        {
            ExceptionHelper.ReturnErrorResponse(logger, httpContext, contextInfo, notfound, HttpStatusCode.NotFound, notfound.Message).GetAwaiter().GetResult();
            return new ValueTask<bool>(true);
        }
        return new ValueTask<bool>(false);
    }
}

