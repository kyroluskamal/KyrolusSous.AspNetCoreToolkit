namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class GeneralExceptionHandler(ILogger<GeneralExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<Exception>(
        logger,
        HttpStatusCode.BadRequest,
        KyrolusErrorCodes.BadRequest,
        "Bad request");
