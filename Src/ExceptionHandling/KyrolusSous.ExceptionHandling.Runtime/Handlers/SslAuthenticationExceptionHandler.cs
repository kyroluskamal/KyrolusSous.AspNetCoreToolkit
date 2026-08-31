namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class SslAuthenticationException : AuthenticationException
{
    public SslAuthenticationException(string message) : base(message) { }
    public SslAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}

public class SslAuthenticationExceptionHandler(
    ILogger<SslAuthenticationExceptionHandler> logger,
    IKyrolusLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<AuthenticationException>(
        logger,
        HttpStatusCode.BadGateway,
        KyrolusErrorCodes.ExternalService,
        "SSL Authentication failed",
        localizer,
        sanitizer,
        contextFactory);
