namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public sealed record KyrolusExceptionMapping(
    KyrolusErrorEnvelope Error,
    HttpStatusCode StatusCode,
    bool IsTransient = false,
    bool ShouldLog = true)
{
    public KyrolusExceptionMapping AsTransient(bool isTransient = true) => this with { IsTransient = isTransient };

    public KyrolusExceptionMapping WithLogging(bool shouldLog) => this with { ShouldLog = shouldLog };

    public KyrolusExceptionMapping WithoutLogging() => this with { ShouldLog = false };

    public static KyrolusExceptionMapping Create(
        string code,
        string title,
        HttpStatusCode statusCode,
        string? detail = null,
        string? traceId = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        => new(
            new KyrolusErrorEnvelope(code, title, detail, traceId, errors, metadata),
            statusCode);
}
