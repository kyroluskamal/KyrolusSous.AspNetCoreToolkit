namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class JsonExceptionHandler(ILogger<JsonExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<JsonException>(
        logger,
        HttpStatusCode.BadRequest,
        KyrolusErrorCodes.InvalidJson,
        "Invalid JSON payload");
