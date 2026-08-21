namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class SslAuthenticationException : AuthenticationException
{
    public SslAuthenticationException(string message) : base(message) { }
    public SslAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}

public class AuthenticationExceptionHandler(ILogger<AuthenticationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is SslAuthenticationException sslAuthenticationException)
        {
            await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
                logger, httpContext, HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService,
                "Authentication failed", sslAuthenticationException.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        return false;
    }
}
