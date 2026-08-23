namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class TimeoutExceptionHandler(ILogger<TimeoutExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<TimeoutException>(
        logger,
        HttpStatusCode.GatewayTimeout,
        KyrolusErrorCodes.Timeout,
        "Request timeout");
