namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entityName, string key) : base($"{entityName} with key {key} not found") { }
}

public class NotFoundExceptionHandler(ILogger<NotFoundExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<NotFoundException>(
        logger,
        HttpStatusCode.NotFound,
        KyrolusErrorCodes.NotFound,
        "Not found");
