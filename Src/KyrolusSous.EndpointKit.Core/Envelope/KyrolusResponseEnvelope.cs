using System.Text.Json.Serialization;
using KyrolusSous.EndpointKit.Core.Hateoas;

namespace KyrolusSous.EndpointKit.Core.Envelope;

/// <summary>
/// Configurable response envelope wrapper for API responses.
/// </summary>
public class KyrolusResponseEnvelope
{
    /// <summary>Creates a new response envelope.</summary>
    public KyrolusResponseEnvelope() { }

    /// <summary>Creates a success envelope with data.</summary>
    public KyrolusResponseEnvelope(object? data, KyrolusResponseMeta? meta = null, IReadOnlyList<KyrolusLink>? links = null)
    {
        Success = true;
        Data = data;
        Meta = meta;
        Links = links;
    }

    /// <summary>Creates an error envelope.</summary>
    public KyrolusResponseEnvelope(string errorCode, string message, IReadOnlyList<KyrolusErrorDetail>? errors = null)
    {
        Success = false;
        Error = new KyrolusResponseError(errorCode, message, errors);
    }

    /// <summary>Indicates if the request was successful.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>The response data (null for error responses).</summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    /// <summary>Response metadata.</summary>
    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KyrolusResponseMeta? Meta { get; set; }

    /// <summary>HATEOAS links.</summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<KyrolusLink>? Links { get; set; }

    /// <summary>Error information (null for success responses).</summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KyrolusResponseError? Error { get; set; }

    /// <summary>Creates a success envelope.</summary>
    public static KyrolusResponseEnvelope Ok(object? data, KyrolusResponseMeta? meta = null, IReadOnlyList<KyrolusLink>? links = null)
        => new(data, meta, links);

    /// <summary>Creates an error envelope.</summary>
    public static KyrolusResponseEnvelope Fail(string errorCode, string message, IReadOnlyList<KyrolusErrorDetail>? errors = null)
        => new(errorCode, message, errors);
}

/// <summary>
/// Response metadata.
/// </summary>
public class KyrolusResponseMeta
{
    /// <summary>HTTP status code.</summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>Response timestamp.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Request trace identifier.</summary>
    [JsonPropertyName("traceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }

    /// <summary>API version.</summary>
    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }

    /// <summary>Total count for paged responses.</summary>
    [JsonPropertyName("totalCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TotalCount { get; set; }

    /// <summary>Page number for paged responses.</summary>
    [JsonPropertyName("page")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Page { get; set; }

    /// <summary>Page size for paged responses.</summary>
    [JsonPropertyName("pageSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PageSize { get; set; }

    /// <summary>Total pages for paged responses.</summary>
    [JsonPropertyName("totalPages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalPages { get; set; }

    /// <summary>Whether there are more items.</summary>
    [JsonPropertyName("hasMore")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasMore { get; set; }
}

/// <summary>
/// Error information in the response envelope.
/// </summary>
public class KyrolusResponseError
{
    public KyrolusResponseError() { }

    public KyrolusResponseError(string code, string message, IReadOnlyList<KyrolusErrorDetail>? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }

    /// <summary>Error code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    /// <summary>Human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;

    /// <summary>Detailed error information.</summary>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<KyrolusErrorDetail>? Details { get; set; }
}

/// <summary>
/// Individual error detail.
/// </summary>
public class KyrolusErrorDetail
{
    public KyrolusErrorDetail() { }

    public KyrolusErrorDetail(string? field, string code, string message)
    {
        Field = field;
        Code = code;
        Message = message;
    }

    /// <summary>Field path that caused the error (null for general errors).</summary>
    [JsonPropertyName("field")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; set; }

    /// <summary>Error code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    /// <summary>Error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;
}

/// <summary>
/// Configuration options for response envelope.
/// </summary>
public class KyrolusEnvelopeOptions
{
    /// <summary>Enable response envelope wrapping (default: true).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Include metadata in responses (default: true).</summary>
    public bool IncludeMeta { get; set; } = true;

    /// <summary>Include timestamp in metadata (default: true).</summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>Include trace ID in metadata (default: true).</summary>
    public bool IncludeTraceId { get; set; } = true;

    /// <summary>Include API version in metadata (default: false).</summary>
    public bool IncludeVersion { get; set; } = false;

    /// <summary>Include pagination info in metadata (default: true).</summary>
    public bool IncludePagination { get; set; } = true;

    /// <summary>HATEOAS options.</summary>
    public KyrolusHateoasOptions Hateoas { get; set; } = new();

    /// <summary>Property name for the data field (default: "data").</summary>
    public string DataPropertyName { get; set; } = "data";

    /// <summary>Property name for the meta field (default: "meta").</summary>
    public string MetaPropertyName { get; set; } = "meta";

    /// <summary>Property name for the error field (default: "error").</summary>
    public string ErrorPropertyName { get; set; } = "error";

    /// <summary>Property name for the links field (default: "_links").</summary>
    public string LinksPropertyName { get; set; } = "_links";

    /// <summary>Endpoints to exclude from envelope wrapping.</summary>
    public HashSet<string> ExcludedEndpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Content types that should use envelope (default: application/json).</summary>
    public HashSet<string> ApplicableContentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json"
    };
}

/// <summary>
/// Builder for creating response envelopes.
/// </summary>
public class KyrolusEnvelopeBuilder
{
    private readonly KyrolusEnvelopeOptions _options;
    private object? _data;
    private int _statusCode = 200;
    private string? _traceId;
    private string? _version;
    private long? _totalCount;
    private int? _page;
    private int? _pageSize;
    private IReadOnlyList<KyrolusLink>? _links;
    private string? _errorCode;
    private string? _errorMessage;
    private IReadOnlyList<KyrolusErrorDetail>? _errorDetails;

    public KyrolusEnvelopeBuilder(KyrolusEnvelopeOptions options)
    {
        _options = options;
    }

    public KyrolusEnvelopeBuilder WithData(object? data)
    {
        _data = data;
        return this;
    }

    public KyrolusEnvelopeBuilder WithStatusCode(int statusCode)
    {
        _statusCode = statusCode;
        return this;
    }

    public KyrolusEnvelopeBuilder WithTraceId(string? traceId)
    {
        _traceId = traceId;
        return this;
    }

    public KyrolusEnvelopeBuilder WithVersion(string? version)
    {
        _version = version;
        return this;
    }

    public KyrolusEnvelopeBuilder WithPagination(long totalCount, int page, int pageSize)
    {
        _totalCount = totalCount;
        _page = page;
        _pageSize = pageSize;
        return this;
    }

    public KyrolusEnvelopeBuilder WithLinks(IReadOnlyList<KyrolusLink>? links)
    {
        _links = links;
        return this;
    }

    public KyrolusEnvelopeBuilder WithError(string code, string message, IReadOnlyList<KyrolusErrorDetail>? details = null)
    {
        _errorCode = code;
        _errorMessage = message;
        _errorDetails = details;
        return this;
    }

    public KyrolusResponseEnvelope Build()
    {
        if (_errorCode is not null)
        {
            return new KyrolusResponseEnvelope
            {
                Success = false,
                Error = new KyrolusResponseError(_errorCode, _errorMessage ?? "An error occurred", _errorDetails),
                Meta = BuildMeta()
            };
        }

        return new KyrolusResponseEnvelope
        {
            Success = true,
            Data = _data,
            Meta = BuildMeta(),
            Links = _options.Hateoas.Enabled ? _links : null
        };
    }

    private KyrolusResponseMeta? BuildMeta()
    {
        if (!_options.IncludeMeta) return null;

        var meta = new KyrolusResponseMeta
        {
            Status = _statusCode
        };

        if (_options.IncludeTimestamp)
            meta.Timestamp = DateTimeOffset.UtcNow;

        if (_options.IncludeTraceId && _traceId is not null)
            meta.TraceId = _traceId;

        if (_options.IncludeVersion && _version is not null)
            meta.Version = _version;

        if (_options.IncludePagination && _totalCount.HasValue)
        {
            meta.TotalCount = _totalCount;
            meta.Page = _page;
            meta.PageSize = _pageSize;
            if (_pageSize.HasValue && _pageSize.Value > 0)
            {
                meta.TotalPages = (int)Math.Ceiling((double)_totalCount.Value / _pageSize.Value);
                meta.HasMore = _page < meta.TotalPages;
            }
        }

        return meta;
    }
}
