namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusUnauthorizedException(string? detail = null, Exception? innerException = null) : KyrolusException(HttpStatusCode.Unauthorized, KyrolusErrorCodes.Unauthorized, "Unauthorized", detail, null, false, innerException)
{
}
