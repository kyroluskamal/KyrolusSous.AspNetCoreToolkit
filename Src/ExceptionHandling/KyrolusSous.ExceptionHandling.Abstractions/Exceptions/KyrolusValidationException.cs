namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusValidationException(IEnumerable<KyrolusErrorItem> errors, string? title = null, string? detail = null) : KyrolusException(HttpStatusCode.BadRequest, KyrolusErrorCodes.Validation, title ?? "Validation failed", detail, errors.ToArray())
{
}
