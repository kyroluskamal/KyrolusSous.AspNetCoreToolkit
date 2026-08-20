namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusTimeoutException(string? detail = null, Exception? innerException = null) 
: KyrolusException(HttpStatusCode.GatewayTimeout, KyrolusErrorCodes.Timeout, "Timeout", detail, null, true, innerException)
{
}
