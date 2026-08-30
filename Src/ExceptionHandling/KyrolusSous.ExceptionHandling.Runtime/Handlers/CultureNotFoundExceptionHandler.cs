namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class CultureNotFoundExceptionHandler(
    ILogger<CultureNotFoundExceptionHandler> logger,
    IKyrolusErrorLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<CultureNotFoundException>(
        logger,
        HttpStatusCode.BadRequest,
        KyrolusErrorCodes.BadRequest,
        "Invalid culture",
        localizer,
        sanitizer,
        contextFactory);
