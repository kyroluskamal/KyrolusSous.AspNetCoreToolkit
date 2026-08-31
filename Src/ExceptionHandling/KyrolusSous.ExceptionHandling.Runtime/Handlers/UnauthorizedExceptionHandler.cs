namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
    public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}

public class UnauthorizedExceptionHandler(
    ILogger<UnauthorizedExceptionHandler> logger,
    IKyrolusLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<UnauthorizedException>(
        logger,
        HttpStatusCode.Unauthorized,
        KyrolusErrorCodes.Unauthorized,
        "Unauthorized",
        localizer,
        sanitizer,
        contextFactory);
