using System.Net.Http;

namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class HttpRequestExceptionHandler(ILogger<HttpRequestExceptionHandler> logger)
    : KyrolusExceptionHandlerBase<HttpRequestException>(
        logger,
        HttpStatusCode.BadGateway,
        KyrolusErrorCodes.ExternalService,
        "External service error");
