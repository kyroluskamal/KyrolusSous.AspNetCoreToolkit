namespace KyrolusSous.RabbitMQ.Abstractions.Models;

/// <summary>
/// CNCF CloudEvents 1.0 compliant message envelope for standardized cross-platform event interchange.
/// </summary>
/// <typeparam name="T">Payload data type.</typeparam>
public sealed class KyrolusCloudEventEnvelope<T>
{
    public string SpecVersion { get; set; } = "1.0";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = "/services/app";
    public string Type { get; set; } = typeof(T).FullName ?? typeof(T).Name;
    public string? Subject { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
    public string DataContentType { get; set; } = "application/json";
    public T? Data { get; set; }
    public Dictionary<string, object?> ExtensionAttributes { get; set; } = [];

    public KyrolusCloudEventEnvelope() { }

    public KyrolusCloudEventEnvelope(T data, string source, string? subject = null)
    {
        Data = data;
        Source = source;
        Subject = subject;
    }
}
