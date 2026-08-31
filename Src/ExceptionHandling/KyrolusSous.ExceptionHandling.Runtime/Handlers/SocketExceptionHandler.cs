namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class SocketExceptionHandler(
    ILogger<SocketExceptionHandler> logger,
    IKyrolusLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<SocketException>(
        logger,
        HttpStatusCode.InternalServerError,
        KyrolusErrorCodes.ExternalService,
        "Socket error",
        localizer,
        sanitizer,
        contextFactory);
