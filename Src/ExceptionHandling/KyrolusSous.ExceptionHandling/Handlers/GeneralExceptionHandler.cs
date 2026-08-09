namespace KyrolusSous.ExceptionHandling.Handlers;

public class GeneralExceptionHandler(ILogger<GeneralExceptionHandler> logger) : IExceptionHandler
{


    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var contextInfo = new ErrorContextInfo(httpContext);

        ExceptionHelper.ReturnErrorResponse(logger, httpContext, contextInfo, exception, HttpStatusCode.BadRequest, "Internal server error").GetAwaiter().GetResult();
        return new ValueTask<bool>(true);
    }
}
