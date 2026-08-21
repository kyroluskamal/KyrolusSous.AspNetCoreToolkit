namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public class KyrolusDomainException : KyrolusException
{
    public KyrolusDomainException(
        string code,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        bool isTransient = false,
        Exception? innerException = null)
        : base(
            ResolveStatusCode(code),
            code,
            ResolveTitle(code),
            detail,
            errors,
            isTransient,
            innerException)
    {
    }

    public KyrolusDomainException(
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        bool isTransient = false,
        Exception? innerException = null)
        : base(statusCode, code, title, detail, errors, isTransient, innerException)
    {
    }

    private static HttpStatusCode ResolveStatusCode(string code)
    {
        return KyrolusErrorCodeRegistry.TryGet(code, out var definition)
            ? definition.StatusCode
            : HttpStatusCode.BadRequest;
    }

    private static string ResolveTitle(string code)
    {
        return KyrolusErrorCodeRegistry.TryGet(code, out var definition)
            ? definition.Title
            : code;
    }
}
