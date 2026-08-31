namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class ArgumentExceptionHandler(
    ILogger<ArgumentExceptionHandler> logger,
    IKyrolusLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<ArgumentException>(
        logger,
        HttpStatusCode.BadRequest,
        KyrolusErrorCodes.BadRequest,
        "Invalid argument",
        localizer,
        sanitizer,
        contextFactory);
