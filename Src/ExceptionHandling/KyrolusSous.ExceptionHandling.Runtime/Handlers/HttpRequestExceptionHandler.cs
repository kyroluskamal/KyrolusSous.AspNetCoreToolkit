namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class HttpRequestExceptionHandler(
    ILogger<HttpRequestExceptionHandler> logger,
    IKyrolusErrorLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<HttpRequestException>(
        logger,
        HttpStatusCode.BadGateway,
        KyrolusErrorCodes.ExternalService,
        "External service error",
        localizer,
        sanitizer,
        contextFactory);
