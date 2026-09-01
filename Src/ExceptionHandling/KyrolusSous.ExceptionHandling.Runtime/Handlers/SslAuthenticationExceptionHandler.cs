namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class SslAuthenticationException : AuthenticationException
{
    public SslAuthenticationException(string message) : base(message) { }
    public SslAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}

public class SslAuthenticationExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<AuthenticationException>(dependencies);
