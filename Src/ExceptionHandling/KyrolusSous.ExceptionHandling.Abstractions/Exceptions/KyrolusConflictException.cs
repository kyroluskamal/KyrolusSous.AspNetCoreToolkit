namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusConflictException(string title, string? detail = null, Exception? innerException = null) : KyrolusException(HttpStatusCode.Conflict, KyrolusErrorCodes.Conflict, title, detail, null, false, innerException)
{
}
