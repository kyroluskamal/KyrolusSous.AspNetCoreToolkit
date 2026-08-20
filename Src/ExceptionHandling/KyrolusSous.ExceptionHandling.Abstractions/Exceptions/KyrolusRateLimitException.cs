namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusRateLimitException(string? detail = null, Exception? innerException = null) 
: KyrolusException((HttpStatusCode)429, KyrolusErrorCodes.RateLimit, "Rate limit exceeded", detail, null, true, innerException)
{
}
