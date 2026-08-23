namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class ArgumentExceptionHandler(ILogger<ArgumentExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<ArgumentException>(
        logger,
        HttpStatusCode.BadRequest,
        KyrolusErrorCodes.BadRequest,
        "Invalid argument");
