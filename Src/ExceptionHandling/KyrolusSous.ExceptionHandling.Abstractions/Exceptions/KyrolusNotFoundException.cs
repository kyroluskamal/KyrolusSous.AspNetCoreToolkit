namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusNotFoundException : KyrolusException
{
    public string? EntityName { get; }
    public string? Key { get; }

    public KyrolusNotFoundException(string entityName, string key, Exception? innerException = null)
        : this(entityName, (object)key, innerException)
    {
    }

    public KyrolusNotFoundException(string entityName, object key, Exception? innerException = null)
        : base(
            HttpStatusCode.NotFound,
            KyrolusErrorCodes.NotFound,
            $"{entityName} not found",
            $"{entityName} with key '{key}' was not found.",
            null,
            new Dictionary<string, object?> { ["entityName"] = entityName, ["key"] = key?.ToString() },
            false,
            false,
            innerException)
    {
        EntityName = entityName;
        Key = key?.ToString();
    }
}
