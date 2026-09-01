namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class HttpRequestExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<HttpRequestException>(dependencies);
