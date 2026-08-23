namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class SocketExceptionHandler(ILogger<SocketExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<SocketException>(
        logger,
        HttpStatusCode.InternalServerError,
        KyrolusErrorCodes.ExternalService,
        "Socket error");
