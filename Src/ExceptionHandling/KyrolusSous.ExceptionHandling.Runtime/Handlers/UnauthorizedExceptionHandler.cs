namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
    public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}

public class UnauthorizedExceptionHandler(ILogger<UnauthorizedExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<UnauthorizedException>(
        logger,
        HttpStatusCode.Unauthorized,
        KyrolusErrorCodes.Unauthorized,
        "Unauthorized");
