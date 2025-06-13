namespace KyrolusSous.ExceptionHandling.Handlers;

public class ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var contextInfo = new ErrorContextInfo(httpContext);
        if (exception is ValidationException validationException)
        {
            ExceptionHelper.ReturnErrorResponse(logger, httpContext, contextInfo, validationException, (HttpStatusCode)450, validationException.Message).GetAwaiter().GetResult();
            return new ValueTask<bool>(true);
        }

        return new ValueTask<bool>(false);
    }
}
