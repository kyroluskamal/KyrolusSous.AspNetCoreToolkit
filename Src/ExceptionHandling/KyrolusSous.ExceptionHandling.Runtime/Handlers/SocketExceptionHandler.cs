namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class SocketExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<SocketException>(dependencies);
