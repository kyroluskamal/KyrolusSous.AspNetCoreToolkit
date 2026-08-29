namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusNotFoundException(string entityName, object key, Exception? innerException = null) : KyrolusException(
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
    public string? EntityName { get; } = entityName;
    public string? Key { get; } = key?.ToString();

    public KyrolusNotFoundException(string entityName, string key, Exception? innerException = null)
        : this(entityName, (object)key, innerException)
    {
    }
}
