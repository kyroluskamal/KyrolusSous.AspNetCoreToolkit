using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// High-performance security middleware defending standalone web APIs against path traversal,
/// HTTP method override spoofing, client certificate header tampering, and header flood DoS attacks.
/// </summary>
public sealed class KyrolusRequestHardeningMiddleware
{
    private static readonly ReadOnlyMemory<byte> Problem400Bytes =
        """{"type":"https://httpstatuses.com/400","title":"Bad Request","status":400,"detail":"Path contains invalid traversal or control characters."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem405Bytes =
        """{"type":"https://httpstatuses.com/405","title":"Method Not Allowed","status":405,"detail":"HTTP method override is not allowed for safe HTTP verbs."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem431Bytes =
        """{"type":"https://httpstatuses.com/431","title":"Request Header Fields Too Large","status":431,"detail":"Request headers exceeded maximum allowed limits."}"""u8.ToArray();

    private static readonly string[] MethodOverrideHeaderNames =
    [
        "X-HTTP-Method-Override",
        "X-HTTP-Method",
        "X-Method-Override"
    ];

    private static readonly string[] ClientCertHeaderNames =
    [
        "X-Client-Cert",
        "X-Client-Cert-Thumbprint",
        "X-Client-Cert-Subject",
        "X-Client-Cert-Issuer"
    ];

    private readonly RequestDelegate _next;
    private readonly KyrolusRequestHardeningOptions _options;

    public KyrolusRequestHardeningMiddleware(
        RequestDelegate next,
        IOptions<KyrolusRequestHardeningOptions>? options = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? new KyrolusRequestHardeningOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Path Traversal & Control Character Validation (HTTP 400)
        if (_options.BlockPathTraversal && HasPathTraversalOrControlChars(context.Request.Path))
        {
            await RejectAsync(context, StatusCodes.Status400BadRequest, Problem400Bytes).ConfigureAwait(false);
            return;
        }

        // 2. Request Header Limits Validation (HTTP 431)
        if (ExceedsHeaderLimits(context.Request.Headers, _options.MaxHeaderCount, _options.MaxTotalHeaderSizeBytes))
        {
            await RejectAsync(context, StatusCodes.Status431RequestHeaderFieldsTooLarge, Problem431Bytes).ConfigureAwait(false);
            return;
        }

        // 3. HTTP Method Override Validation & Stripping (HTTP 405)
        if (HasMethodOverrideHeader(context.Request.Headers, out var overrideMethod))
        {
            if (_options.BlockSafeVerbMethodOverride && IsSafeHttpVerb(context.Request.Method) && !string.IsNullOrEmpty(overrideMethod))
            {
                await RejectAsync(context, StatusCodes.Status405MethodNotAllowed, Problem405Bytes).ConfigureAwait(false);
                return;
            }

            if (_options.StripMethodOverrideHeaders)
            {
                StripHeaders(context.Request.Headers, MethodOverrideHeaderNames);
            }
        }

        // 4. Untrusted Client Certificate Headers Stripping
        if (_options.StripUntrustedClientCertHeaders)
        {
            StripHeaders(context.Request.Headers, ClientCertHeaderNames);
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool HasPathTraversalOrControlChars(PathString path)
    {
        if (!path.HasValue) return false;

        var raw = path.Value!;
        if (raw.Contains('\0')) return true;

        if (raw.Contains("..", StringComparison.Ordinal) ||
            raw.Contains(@"..\") ||
            raw.Contains("../", StringComparison.Ordinal) ||
            raw.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("%00", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var unescaped = Uri.UnescapeDataString(raw);
            if (unescaped.Contains('\0')) return true;

            if (unescaped.Contains("..", StringComparison.Ordinal) ||
                unescaped.Contains(@"..\") ||
                unescaped.Contains("../", StringComparison.Ordinal))
            {
                return true;
            }
        }
        catch (Exception)
        {
            return true;
        }

        return false;
    }

    private static bool ExceedsHeaderLimits(IHeaderDictionary headers, int maxCount, int maxSizeBytes)
    {
        if (headers.Count > maxCount) return true;

        var totalSize = 0;
        foreach (var header in headers)
        {
            totalSize += Encoding.UTF8.GetByteCount(header.Key);
            var headerValues = header.Value;
            for (var i = 0; i < headerValues.Count; i++)
            {
                var val = headerValues[i];
                if (val is not null)
                {
                    totalSize += Encoding.UTF8.GetByteCount(val);
                }
            }

            if (totalSize > maxSizeBytes) return true;
        }

        return false;
    }

    private static bool HasMethodOverrideHeader(IHeaderDictionary headers, out string? overrideMethod)
    {
        for (var i = 0; i < MethodOverrideHeaderNames.Length; i++)
        {
            if (headers.TryGetValue(MethodOverrideHeaderNames[i], out var values) && values.Count > 0)
            {
                overrideMethod = values[0];
                return true;
            }
        }

        overrideMethod = null;
        return false;
    }

    private static bool IsSafeHttpVerb(string method) =>
        HttpMethods.IsGet(method) ||
        HttpMethods.IsHead(method) ||
        HttpMethods.IsOptions(method);

    private static void StripHeaders(IHeaderDictionary headers, string[] namesToStrip)
    {
        for (var i = 0; i < namesToStrip.Length; i++)
        {
            headers.Remove(namesToStrip[i]);
        }
    }

    private static async Task RejectAsync(HttpContext context, int statusCode, ReadOnlyMemory<byte> problemBytes)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.Body.WriteAsync(problemBytes).ConfigureAwait(false);
    }
}
