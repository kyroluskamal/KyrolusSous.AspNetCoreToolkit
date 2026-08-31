namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class JsonExceptionHandler(
    ILogger<JsonExceptionHandler> logger,
    IKyrolusLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<JsonException>(
        logger,
        HttpStatusCode.BadRequest,
        KyrolusErrorCodes.InvalidJson,
        "Invalid JSON payload",
        localizer,
        sanitizer,
        contextFactory);
