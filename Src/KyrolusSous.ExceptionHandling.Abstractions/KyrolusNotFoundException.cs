namespace KyrolusSous.ExceptionHandling.Abstractions;

public sealed class KyrolusNotFoundException : Exception
{
    public KyrolusNotFoundException(string entityName, string key)
        : base($"{entityName} with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }
    public string Key { get; }
}
