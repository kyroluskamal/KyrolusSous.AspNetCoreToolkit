namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusNotFoundException : KyrolusException
{
    public string? EntityName { get; }
    public string? Key { get; }

    public KyrolusNotFoundException(string title, string? detail = null, Exception? innerException = null)
        : base(HttpStatusCode.NotFound, KyrolusErrorCodes.NotFound, title, detail, null, false, innerException)
    {
    }

    public KyrolusNotFoundException(string entityName, object key, Exception? innerException = null)
        : base(
            HttpStatusCode.NotFound,
            KyrolusErrorCodes.NotFound,
            $"{entityName} not found",
            $"{entityName} with key '{key}' was not found.",
            null,
            false,
            innerException)
    {
        EntityName = entityName;
        Key = key?.ToString();
    }
}
