namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusBadRequestException : KyrolusException
{
    public KyrolusBadRequestException(string title, string? detail = null, Exception? innerException = null)
        : base(HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, title, detail, null, false, innerException)
    {
    }
}
