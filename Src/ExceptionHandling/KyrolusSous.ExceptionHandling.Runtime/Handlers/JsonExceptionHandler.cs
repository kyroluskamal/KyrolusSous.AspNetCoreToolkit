namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class JsonExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<JsonException>(dependencies);
