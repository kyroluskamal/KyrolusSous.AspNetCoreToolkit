namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public abstract class KyrolusException : Exception
{
    protected KyrolusException(
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        bool isTransient = false,
        Exception? innerException = null)
        : base(detail ?? title, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        Title = title;
        Detail = detail;
        Errors = errors;
        IsTransient = isTransient;
    }

    public HttpStatusCode StatusCode { get; }
    public string Code { get; }
    public string Title { get; }
    public string? Detail { get; }
    public IReadOnlyList<KyrolusErrorItem>? Errors { get; }
    public bool IsTransient { get; }
}
