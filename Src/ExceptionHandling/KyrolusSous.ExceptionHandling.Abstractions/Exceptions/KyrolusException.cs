namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public abstract class KyrolusException : Exception
{
    private Dictionary<string, object?>? _metadata;
    private List<KyrolusErrorItem>? _errors;

    public HttpStatusCode StatusCode { get; }
    public string Code { get; }
    public string Title { get; }
    public string? Detail { get; protected set; }
    public IReadOnlyList<KyrolusErrorItem>? Errors => _errors;
    public IReadOnlyDictionary<string, object?>? Metadata => _metadata;
    public bool IsTransient { get; protected set; }
    public bool ShouldLog { get; protected set; }

    protected KyrolusException(
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        bool isTransient = false,
        bool shouldLog = true,
        Exception? innerException = null) : base(detail ?? title, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        Title = title;
        Detail = detail;
        IsTransient = isTransient;
        ShouldLog = shouldLog;

        if (errors is { Count: > 0 })
        {
            _errors = [.. errors];
        }

        if (metadata is { Count: > 0 })
        {
            _metadata = new Dictionary<string, object?>(metadata, StringComparer.OrdinalIgnoreCase);
        }
    }

    protected KyrolusException(
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail,
        IReadOnlyList<KyrolusErrorItem>? errors,
        bool isTransient,
        Exception? innerException = null)
        : this(statusCode, code, title, detail, errors, null, isTransient, true, innerException)
    {
    }

    public KyrolusException WithMetadata(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        _metadata[key] = value;
        return this;
    }

    public KyrolusException WithMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        _metadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in metadata)
        {
            _metadata[k] = v;
        }
        return this;
    }

    public KyrolusException WithError(string field, string message, string? code = null)
    {
        _errors ??= [];
        _errors.Add(new KyrolusErrorItem(field, code, message));
        return this;
    }

    public KyrolusException WithoutLogging()
    {
        ShouldLog = false;
        return this;
    }

    public KyrolusException WithLogging(bool shouldLog = true)
    {
        ShouldLog = shouldLog;
        return this;
    }

    public KyrolusException AsTransient(bool isTransient = true)
    {
        IsTransient = isTransient;
        return this;
    }

    public KyrolusException WithDetail(string detail)
    {
        Detail = detail;
        return this;
    }
}
