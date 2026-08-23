namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class CultureNotFoundExceptionHandler(ILogger<CultureNotFoundExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<CultureNotFoundException>(
        logger,
        HttpStatusCode.BadRequest,
        KyrolusErrorCodes.BadRequest,
        "Invalid culture");
