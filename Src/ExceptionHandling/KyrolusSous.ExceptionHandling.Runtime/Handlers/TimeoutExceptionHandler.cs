namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class TimeoutExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<TimeoutException>(dependencies);
