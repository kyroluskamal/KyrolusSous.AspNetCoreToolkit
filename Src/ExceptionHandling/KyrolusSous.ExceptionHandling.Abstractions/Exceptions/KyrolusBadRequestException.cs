namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusBadRequestException(string title, string? detail = null, Exception? innerException = null) 
    : KyrolusException(HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, title, detail, null, null, false, false, innerException)
{
}
