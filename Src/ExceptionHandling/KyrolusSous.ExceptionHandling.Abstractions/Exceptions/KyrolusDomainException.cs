namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public class KyrolusDomainException : KyrolusException
{
    public KyrolusDomainException(
        string code,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        bool? isTransient = null,
        bool? shouldLog = null,
        Exception? innerException = null)
        : base(
            ResolveStatusCode(code),
            code,
            ResolveTitle(code),
            detail,
            errors,
            metadata,
            isTransient ?? ResolveIsTransient(code),
            shouldLog ?? ResolveShouldLog(code),
            innerException)
    {
    }

    public KyrolusDomainException(
        string code,
        string? detail,
        IReadOnlyList<KyrolusErrorItem>? errors,
        bool isTransient,
        Exception? innerException = null)
        : this(code, detail, errors, null, isTransient, null, innerException)
    {
    }

    public KyrolusDomainException(
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        bool isTransient = false,
        bool shouldLog = true,
        Exception? innerException = null)
        : base(statusCode, code, title, detail, errors, metadata, isTransient, shouldLog, innerException)
    {
    }

    private static HttpStatusCode ResolveStatusCode(string code)
        => KyrolusErrorCodeRegistry.TryGet(code, out var definition) ? definition.StatusCode : HttpStatusCode.BadRequest;

    private static string ResolveTitle(string code)
        => KyrolusErrorCodeRegistry.TryGet(code, out var definition) ? definition.Title : code;

    private static bool ResolveIsTransient(string code)
        => KyrolusErrorCodeRegistry.TryGet(code, out var definition) && definition.IsTransient;

    private static bool ResolveShouldLog(string code)
        => !KyrolusErrorCodeRegistry.TryGet(code, out var definition) || definition.ShouldLog;
}
