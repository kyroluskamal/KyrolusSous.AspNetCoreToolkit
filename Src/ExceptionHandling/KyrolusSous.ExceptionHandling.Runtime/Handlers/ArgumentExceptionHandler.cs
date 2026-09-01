namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class ArgumentExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<ArgumentException>(dependencies);
