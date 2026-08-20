namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public abstract class KyrolusException(
    HttpStatusCode statusCode,
    string code,
    string title,
    string? detail = null,
    IReadOnlyList<KyrolusErrorItem>? errors = null,
    bool isTransient = false,
    Exception? innerException = null) : Exception(detail ?? title, innerException)
{

    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
    public IReadOnlyList<KyrolusErrorItem>? Errors { get; } = errors;
    public bool IsTransient { get; } = isTransient;
}
