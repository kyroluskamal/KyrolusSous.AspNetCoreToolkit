using System.Text;
using Microsoft.Extensions.Options;

namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// High-performance security middleware defending standalone web APIs against path traversal,
/// HTTP method override spoofing, client certificate header tampering, and header flood DoS attacks.
/// </summary>
public sealed class KyrolusRequestHardeningMiddleware
{
    private static readonly ReadOnlyMemory<byte> Problem400SmugglingBytes =
        """{"type":"https://httpstatuses.com/400","title":"Bad Request","status":400,"detail":"Conflicting or duplicate content transfer headers detected (HTTP Request Smuggling defense)."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem400TraversalBytes =
        """{"type":"https://httpstatuses.com/400","title":"Bad Request","status":400,"detail":"Path traversal or invalid characters detected in the request path or query."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem400HostBytes =
        """{"type":"https://httpstatuses.com/400","title":"Bad Request","status":400,"detail":"The request host header is invalid or untrusted."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem403ForbiddenBytes =
        """{"type":"https://httpstatuses.com/403","title":"Forbidden","status":403,"detail":"Access from your IP address is restricted."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem405Bytes =
        """{"type":"https://httpstatuses.com/405","title":"Method Not Allowed","status":405,"detail":"HTTP method override is not allowed for safe HTTP verbs."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem405DangerousVerbBytes =
        """{"type":"https://httpstatuses.com/405","title":"Method Not Allowed","status":405,"detail":"The specified HTTP verb is dangerous and forbidden."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem413Bytes =
        """{"type":"https://httpstatuses.com/413","title":"Payload Too Large","status":413,"detail":"Request payload exceeds maximum allowed size."}"""u8.ToArray();

    private static readonly ReadOnlyMemory<byte> Problem415UnsupportedMediaTypeBytes =
        """{"type":"https://httpstatuses.com/415","title":"Unsupported Media Type","status":415,"detail":"The request content type is not supported."}"""u8.ToArray();

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
        "X-Client-Cert-Issuer",
        "X-SSL-Client-Verify",
        "X-SSL-Client-S-DN"
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

        // 1. Dangerous Verbs Blocking (TRACE, TRACK, CONNECT) (HTTP 405)
        if (_options.BlockDangerousVerbs && IsDangerousVerb(context.Request.Method))
        {
            await RejectAsync(context, StatusCodes.Status405MethodNotAllowed, Problem405DangerousVerbBytes).ConfigureAwait(false);
            return;
        }

        // 2. Request Header Limits Validation (HTTP 431)
        if (ExceedsHeaderLimits(context.Request.Headers, _options.MaxHeaderCount, _options.MaxTotalHeaderSizeBytes))
        {
            await RejectAsync(context, StatusCodes.Status431RequestHeaderFieldsTooLarge, Problem431Bytes).ConfigureAwait(false);
            return;
        }

        // 3. Early Payload Size Validation (HTTP 413) & Kestrel Stream Limit Binding
        if (_options.MaxRequestBodySizeBytes.HasValue)
        {
            var maxBodySizeFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (maxBodySizeFeature is { IsReadOnly: false })
            {
                maxBodySizeFeature.MaxRequestBodySize = _options.MaxRequestBodySizeBytes.Value;
            }

            if (context.Request.ContentLength.HasValue &&
                context.Request.ContentLength.Value > _options.MaxRequestBodySizeBytes.Value)
            {
                await RejectAsync(context, StatusCodes.Status413PayloadTooLarge, Problem413Bytes).ConfigureAwait(false);
                return;
            }
        }

        // 4. Content-Type Whitelist Validation (HTTP 415 / CWE-436 / RFC 7231)
        if (_options.AllowedContentTypes is { Count: > 0 })
        {
            var hasContent = (context.Request.ContentLength is > 0) ||
                             context.Request.Headers.ContainsKey("Transfer-Encoding");

            if (hasContent)
            {
                var contentType = context.Request.ContentType;
                if (string.IsNullOrWhiteSpace(contentType) || !IsAllowedContentType(contentType, _options.AllowedContentTypes))
                {
                    await RejectAsync(context, StatusCodes.Status415UnsupportedMediaType, Problem415UnsupportedMediaTypeBytes).ConfigureAwait(false);
                    return;
                }
            }
        }

        // 5. Host Header Validation (HTTP 400)
        if (_options.AllowedHosts is { Count: > 0 } && !IsAllowedHost(context.Request.Host.Host, _options.AllowedHosts))
        {
            await RejectAsync(context, StatusCodes.Status400BadRequest, Problem400HostBytes).ConfigureAwait(false);
            return;
        }

        // 6. IP Address Filtering (HTTP 403)
        if ((_options.BlockedIpsOrCidrs is { Count: > 0 } || _options.AllowedIpsOrCidrs is { Count: > 0 }) &&
            IsIpRestricted(context.Connection.RemoteIpAddress, _options.AllowedIpsOrCidrs, _options.BlockedIpsOrCidrs))
        {
            await RejectAsync(context, StatusCodes.Status403Forbidden, Problem403ForbiddenBytes).ConfigureAwait(false);
            return;
        }

        // 7. HTTP Request Smuggling Detection (CWE-444 / RFC 7230 / RFC 9112) (HTTP 400)
        if (_options.BlockRequestSmuggling && IsRequestSmugglingAttempt(context.Request.Headers))
        {
            await RejectAsync(context, StatusCodes.Status400BadRequest, Problem400SmugglingBytes).ConfigureAwait(false);
            return;
        }

        // 7. Path Traversal & Control Character Validation in Path & QueryString (HTTP 400)
        if (_options.BlockPathTraversal && HasPathTraversalOrControlChars(context.Request.Path, context.Request.QueryString, _options.InspectQueryStringForTraversal))
        {
            await RejectAsync(context, StatusCodes.Status400BadRequest, Problem400TraversalBytes).ConfigureAwait(false);
            return;
        }

        // 8. HTTP Method Override Validation & Stripping (HTTP 405)
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

        // 9. Untrusted Client Certificate Headers Stripping
        if (_options.StripUntrustedClientCertHeaders)
        {
            StripHeaders(context.Request.Headers, ClientCertHeaderNames);
        }

        await _next(context).ConfigureAwait(false);
    }

    internal static bool HasPathTraversalOrControlChars(PathString path)
        => HasPathTraversalOrControlChars(path, QueryString.Empty, inspectQueryString: false);

    internal static bool HasPathTraversalOrControlChars(PathString path, QueryString queryString, bool inspectQueryString)
    {
        if (path.HasValue && ContainsPathTraversal(path.Value))
        {
            return true;
        }

        if (inspectQueryString && queryString.HasValue && ContainsPathTraversal(queryString.Value))
        {
            return true;
        }

        return false;
    }

    internal static bool ContainsPathTraversal(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        // 1. Defend against Null-Byte Injection (%00, \0)
        if (path.Contains('\0') || path.Contains("%00", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Defend against raw unnormalized dot-segment traversals
        if (path.Contains("/../", StringComparison.Ordinal) ||
            path.EndsWith("/..", StringComparison.Ordinal) ||
            path.StartsWith("../", StringComparison.Ordinal) ||
            string.Equals(path, "..", StringComparison.Ordinal))
        {
            return true;
        }

        // 3. Defend against encoded dot segments and mixed slash encodings (..%2f, %2e%2e, ..%5c)
        if (path.Contains("..%2f", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("..%5c", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2f..", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%5c..", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2e.", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".%2e", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 4. Defend against Windows/IIS backslash traversal bypasses
        if (path.Contains(@"\..\") || path.EndsWith(@"\..") || path.Contains(@"\"))
        {
            return true;
        }

        // 5. Defend against Semicolon / Matrix Parameter Traversal bypasses (CVE-2020-5410 / CVE-2018-1271)
        if (path.Contains("..;", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(";..", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/..;/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/;../", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/.;/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("..%3b", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%3b..", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 6. Deep inspection on unescaped content to catch double-encoded or alternate bypasses
        try
        {
            var unescaped = Uri.UnescapeDataString(path);
            if (unescaped.Contains('\0') ||
                unescaped.Contains("/../", StringComparison.Ordinal) ||
                unescaped.EndsWith("/..", StringComparison.Ordinal) ||
                unescaped.StartsWith("../", StringComparison.Ordinal) ||
                unescaped.Contains("..;", StringComparison.Ordinal) ||
                unescaped.Contains(";..", StringComparison.Ordinal) ||
                unescaped.Contains("/..;/", StringComparison.Ordinal) ||
                unescaped.Contains("/;../", StringComparison.Ordinal) ||
                unescaped.Contains("/.;/", StringComparison.Ordinal) ||
                unescaped.Contains(@"\"))
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

    private static bool IsAllowedContentType(string contentType, IReadOnlyList<string> allowedTypes)
    {
        var mime = contentType.Split(';', StringSplitOptions.TrimEntries)[0];
        for (var i = 0; i < allowedTypes.Count; i++)
        {
            if (string.Equals(mime, allowedTypes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool IsRequestSmugglingAttempt(IHeaderDictionary headers)
    {
        // 1. Conflicting Transfer-Encoding AND Content-Length headers (CL.TE / TE.CL attack vector)
        var hasTransferEncoding = headers.TryGetValue("Transfer-Encoding", out var teValues) && teValues.Count > 0;
        var hasContentLength = headers.TryGetValue("Content-Length", out var clValues) && clValues.Count > 0;

        if (hasTransferEncoding && hasContentLength)
        {
            return true;
        }

        // 2. Multiple differing Content-Length headers (RFC 7230 § 3.3.2)
        if (hasContentLength && clValues.Count > 1)
        {
            var first = clValues[0]?.Trim();
            for (var i = 1; i < clValues.Count; i++)
            {
                if (!string.Equals(first, clValues[i]?.Trim(), StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        // 3. Obfuscated or invalid Transfer-Encoding headers (e.g. control characters)
        if (hasTransferEncoding)
        {
            for (var i = 0; i < teValues.Count; i++)
            {
                var val = teValues[i];
                if (val is null) continue;

                if (val.Contains('\0') || val.Contains('\r') || val.Contains('\n'))
                {
                    return true;
                }
            }
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

    private static bool IsDangerousVerb(string method) =>
        string.Equals(method, "TRACE", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "TRACK", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedHost(string host, IReadOnlyList<string> allowedHosts)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        for (var i = 0; i < allowedHosts.Count; i++)
        {
            var pattern = allowedHosts[i];
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            if (pattern == "*")
            {
                return true;
            }

            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = pattern[1..];
                if (host.Length > suffix.Length && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsIpRestricted(System.Net.IPAddress? remoteIp, IReadOnlyList<string>? allowedList, IReadOnlyList<string>? blockedList)
    {
        if (remoteIp is null)
        {
            return allowedList is { Count: > 0 };
        }

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        if (blockedList is { Count: > 0 } && MatchesAnyNetwork(remoteIp, blockedList))
        {
            return true;
        }

        if (allowedList is { Count: > 0 } && !MatchesAnyNetwork(remoteIp, allowedList))
        {
            return true;
        }

        return false;
    }

    private static bool MatchesAnyNetwork(System.Net.IPAddress ip, IReadOnlyList<string> networks)
    {
        for (var i = 0; i < networks.Count; i++)
        {
            var part = networks[i];
            if (string.IsNullOrWhiteSpace(part)) continue;

            if (System.Net.IPNetwork.TryParse(part, out var network))
            {
                if (network.Contains(ip)) return true;
            }
            else if (System.Net.IPAddress.TryParse(part, out var parsedIp))
            {
                var prefix = parsedIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
                if (new System.Net.IPNetwork(parsedIp, prefix).Contains(ip)) return true;
            }
        }

        return false;
    }
}
