namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusForbiddenException(string? detail = null, Exception? innerException = null) : KyrolusException(HttpStatusCode.Forbidden, KyrolusErrorCodes.Forbidden, "Forbidden", detail, null, false, innerException)
{
}
