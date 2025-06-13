namespace KyrolusSous.ExceptionHandling.Handlers;

public class NpgsqlExceptionHandler(ILogger<NpgsqlExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var contextInfo = new ErrorContextInfo(httpContext);
        if (exception is NpgsqlException NpgsqlException)
        {
            ExceptionHelper.ReturnErrorResponse(logger, httpContext, contextInfo, NpgsqlException, HttpStatusCode.InternalServerError, "Npgsql is not found").GetAwaiter().GetResult();
            return new ValueTask<bool>(true);
        }
        return new ValueTask<bool>(false);
    }
}
