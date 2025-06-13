namespace KyrolusSous.ExceptionHandling.Handlers;

public class SslAuthenticationException : AuthenticationException
{
    public SslAuthenticationException(string message) : base(message)
    {
    }

    public SslAuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
public class AuthenticationExceptionHandler(ILogger<SocketExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var contextInfo = new ErrorContextInfo(httpContext);
        if (exception is SslAuthenticationException sslAuthenticationException)
        {
            ExceptionHelper.ReturnErrorResponse(logger, httpContext, contextInfo, sslAuthenticationException, HttpStatusCode.BadGateway, sslAuthenticationException.Message).GetAwaiter().GetResult();
            return new ValueTask<bool>(true);
        }
        return new ValueTask<bool>(false);
    }
}
