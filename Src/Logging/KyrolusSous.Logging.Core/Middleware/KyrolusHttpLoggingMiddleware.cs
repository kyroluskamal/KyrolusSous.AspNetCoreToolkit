using System.Diagnostics;
using System.Text;
using KyrolusSous.Logging.Core.Correlation;
using KyrolusSous.Logging.Core.Masking;
using KyrolusSous.Logging.Core.Redaction;

namespace KyrolusSous.Logging.Core.Middleware;

/// <summary>
/// Enterprise HTTP request and response logging middleware with correlation ID propagation and PII sanitization.
/// </summary>
public sealed class KyrolusHttpLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IKyrolusLogger<KyrolusHttpLoggingMiddleware> _logger;
    private readonly IKyrolusDataMasker _masker;
    private readonly IKyrolusStringRedactor _stringRedactor;
    private readonly KyrolusHttpLoggingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusHttpLoggingMiddleware"/> class.
    /// </summary>
    public KyrolusHttpLoggingMiddleware(
        RequestDelegate next,
        IKyrolusLogger<KyrolusHttpLoggingMiddleware> logger,
        IKyrolusDataMasker masker,
        IOptions<KyrolusHttpLoggingOptions> options,
        IKyrolusStringRedactor? stringRedactor = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _masker = masker ?? throw new ArgumentNullException(nameof(masker));
        _options = options?.Value ?? new KyrolusHttpLoggingOptions();
        _stringRedactor = stringRedactor ?? new KyrolusStringRedactor();
    }

    /// <summary>
    /// Invokes the middleware to log HTTP request execution and propagate correlation context.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsPathExcluded(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var correlationId = ResolveOrGenerateCorrelationId(context);
        var tenantId = ResolveTenantId(context);

        context.Response.Headers.TryAdd(_options.CorrelationHeaderName, correlationId);

        using (KyrolusCorrelationContext.BeginScope(correlationId, tenantId, context.User.Identity?.Name))
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            string? requestBody = null;

            if (_options.IncludeRequestBody && context.Request.ContentLength > 0)
            {
                requestBody = await ReadRequestBodyAsync(context.Request).ConfigureAwait(false);
            }

            Stream? originalResponseBodyStream = null;
            MemoryStream? responseMemoryStream = null;

            if (_options.IncludeResponseBody)
            {
                originalResponseBodyStream = context.Response.Body;
                responseMemoryStream = new MemoryStream();
                context.Response.Body = responseMemoryStream;
            }

            try
            {
                await _next(context).ConfigureAwait(false);
                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

                string? responseBody = null;
                if (_options.IncludeResponseBody && responseMemoryStream is not null && originalResponseBodyStream is not null)
                {
                    responseBody = await ReadResponseBodyAsync(responseMemoryStream, originalResponseBodyStream).ConfigureAwait(false);
                }

                LogCompletedRequest(context, elapsedMs, correlationId, tenantId, requestBody, responseBody);
            }
            catch (Exception ex)
            {
                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                LogFailedRequest(context, elapsedMs, correlationId, tenantId, ex);

                if (_options.IncludeResponseBody && responseMemoryStream is not null && originalResponseBodyStream is not null)
                {
                    responseMemoryStream.Position = 0;
                    await responseMemoryStream.CopyToAsync(originalResponseBodyStream).ConfigureAwait(false);
                }

                throw;
            }
            finally
            {
                if (originalResponseBodyStream is not null)
                {
                    context.Response.Body = originalResponseBodyStream;
                }
                responseMemoryStream?.Dispose();
            }
        }
    }

    private bool IsPathExcluded(PathString path)
    {
        var pathValue = path.Value;
        if (string.IsNullOrEmpty(pathValue))
        {
            return false;
        }

        return _options.ExcludedPaths.Any(excluded => pathValue.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue("Kyrolus_CorrelationId", out var itemVal) && itemVal is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        if (context.Request.Headers.TryGetValue(_options.CorrelationHeaderName, out var headerVal) &&
            !string.IsNullOrWhiteSpace(headerVal))
        {
            return headerVal.ToString();
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private string? ResolveTenantId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(_options.TenantHeaderName, out var headerVal) &&
            !string.IsNullOrWhiteSpace(headerVal))
        {
            return headerVal.ToString();
        }

        return null;
    }

    private async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        if (body.Length > _options.MaxBodyLength)
        {
            body = body.Substring(0, _options.MaxBodyLength) + "... [Truncated]";
        }

        return _options.MaskSensitiveData ? SanitizeBody(body) : body;
    }

    private async Task<string?> ReadResponseBodyAsync(MemoryStream responseMemoryStream, Stream originalResponseBodyStream)
    {
        responseMemoryStream.Position = 0;
        using var reader = new StreamReader(responseMemoryStream, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);

        responseMemoryStream.Position = 0;
        await responseMemoryStream.CopyToAsync(originalResponseBodyStream).ConfigureAwait(false);

        if (body.Length > _options.MaxBodyLength)
        {
            body = body.Substring(0, _options.MaxBodyLength) + "... [Truncated]";
        }

        return _options.MaskSensitiveData ? SanitizeBody(body) : body;
    }

    private string SanitizeBody(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        return _stringRedactor.Redact(content);
    }

    private void LogCompletedRequest(
        HttpContext context,
        double elapsedMs,
        string correlationId,
        string? tenantId,
        string? requestBody,
        string? responseBody)
    {
        var statusCode = context.Response.StatusCode;
        var level = statusCode >= 500 ? LogLevel.Error : (statusCode >= 400 ? LogLevel.Warning : _options.LogLevel);

        var props = new Dictionary<string, object?>
        {
            ["HttpMethod"] = context.Request.Method,
            ["HttpPath"] = context.Request.Path.Value ?? "/",
            ["HttpStatusCode"] = statusCode,
            ["ElapsedMilliseconds"] = elapsedMs,
            ["CorrelationId"] = correlationId,
            ["ClientIp"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };

        if (!string.IsNullOrEmpty(tenantId))
        {
            props["TenantId"] = tenantId;
        }

        if (!string.IsNullOrEmpty(requestBody))
        {
            props["RequestBody"] = requestBody;
        }

        if (!string.IsNullOrEmpty(responseBody))
        {
            props["ResponseBody"] = responseBody;
        }

        var message = "HTTP {HttpMethod} {HttpPath} responded {HttpStatusCode} in {ElapsedMilliseconds:F2}ms";
        _logger.Log(level, message, null, props);
    }

    private void LogFailedRequest(
        HttpContext context,
        double elapsedMs,
        string correlationId,
        string? tenantId,
        Exception ex)
    {
        var props = new Dictionary<string, object?>
        {
            ["HttpMethod"] = context.Request.Method,
            ["HttpPath"] = context.Request.Path.Value ?? "/",
            ["HttpStatusCode"] = 500,
            ["ElapsedMilliseconds"] = elapsedMs,
            ["CorrelationId"] = correlationId,
            ["ClientIp"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };

        if (!string.IsNullOrEmpty(tenantId))
        {
            props["TenantId"] = tenantId;
        }

        var message = "HTTP {HttpMethod} {HttpPath} failed in {ElapsedMilliseconds:F2}ms with exception";
        _logger.Log(LogLevel.Error, message, ex, props);
    }
}
