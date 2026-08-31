namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class TimeoutExceptionHandler(
    ILogger<TimeoutExceptionHandler> logger,
    IKyrolusLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null)
    : KyrolusExceptionHandlerBase<TimeoutException>(
        logger,
        HttpStatusCode.GatewayTimeout,
        KyrolusErrorCodes.Timeout,
        "Request timeout",
        localizer,
        sanitizer,
        contextFactory);
