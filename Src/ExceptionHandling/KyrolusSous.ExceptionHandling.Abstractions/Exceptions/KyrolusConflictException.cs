namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusConflictException : KyrolusException
{
    public KyrolusConflictException(string title, string? detail = null, Exception? innerException = null)
        : base(HttpStatusCode.Conflict, KyrolusErrorCodes.Conflict, title, detail, null, false, innerException)
    {
    }
}
