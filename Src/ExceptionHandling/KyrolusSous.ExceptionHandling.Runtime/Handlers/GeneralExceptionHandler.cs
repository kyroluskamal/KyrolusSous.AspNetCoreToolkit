namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class GeneralExceptionHandler(
    ILogger<GeneralExceptionHandler> logger,
    IKyrolusLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<Exception>(
        logger,
        HttpStatusCode.BadRequest,
        KyrolusErrorCodes.BadRequest,
        "Bad request",
        localizer,
        sanitizer,
        contextFactory);
