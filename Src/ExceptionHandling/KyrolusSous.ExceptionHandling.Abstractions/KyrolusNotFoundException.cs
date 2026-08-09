namespace KyrolusSous.ExceptionHandling.Abstractions;

public sealed class KyrolusNotFoundException(string entityName, string key) : KyrolusException(
        HttpStatusCode.NotFound,
        KyrolusErrorCodes.NotFound,
        $"{entityName} not found",
        $"{entityName} with key '{key}' was not found.")
{

    public string EntityName { get; } = entityName;
    public string Key { get; } = key;
}
