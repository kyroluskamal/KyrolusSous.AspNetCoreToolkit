namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class CultureNotFoundExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<CultureNotFoundException>(dependencies);
