namespace KyrolusSous.RateLimiting;

/// <summary>
/// Payload sizes, multipart uploads, compression encodings, and content digest partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a partition key based on request payload size buckets (<c>"payload_empty"</c>, <c>"payload_small"</c>, <c>"payload_medium"</c>, <c>"payload_large"</c>),
    /// preventing heavy multipart/form-data and large JSON payloads from monopolizing gateway network bandwidth and buffer memory.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="smallThresholdBytes">Maximum bytes for small payloads. Defaults to 32 KB (32,768 bytes).</param>
    /// <param name="mediumThresholdBytes">Maximum bytes for medium payloads. Defaults to 1 MB (1,048,576 bytes).</param>
    /// <returns>The payload size bucket key.</returns>
    public static string GetPayloadSizeBucketKey(HttpContext context, long smallThresholdBytes = 32 * 1024, long mediumThresholdBytes = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(smallThresholdBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(mediumThresholdBytes, smallThresholdBytes);

        var length = context.Request.ContentLength;
        if (!length.HasValue || length.Value == 0)
            return "payload_empty";

        if (length.Value <= smallThresholdBytes)
            return "payload_small";

        if (length.Value <= mediumThresholdBytes)
            return "payload_medium";

        return "payload_large";
    }

    /// <summary>
    /// Resolves a compound partition key combining the request payload size bucket with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="smallThresholdBytes">Maximum bytes for small payloads. Defaults to 32 KB.</param>
    /// <param name="mediumThresholdBytes">Maximum bytes for medium payloads. Defaults to 1 MB.</param>
    /// <returns>A compound key formatted as <c>"{payloadBucket}:{clientKey}"</c>.</returns>
    public static string GetPayloadSizeBucketAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        long smallThresholdBytes = 32 * 1024,
        long mediumThresholdBytes = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bucket = GetPayloadSizeBucketKey(context, smallThresholdBytes, mediumThresholdBytes);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{bucket}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the HTTP <c>Accept</c> request header,
    /// segregating memory-heavy binary export media formats (PDF, Excel, Zip) from lightweight JSON data APIs to prevent Out-Of-Memory (OOM) failures.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"media_heavy"</c> for binary/export types, <c>"media_standard"</c> for standard JSON/HTML, or <c>"media_default"</c>.</returns>
    public static string GetAcceptMediaTypeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Accept", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().ToLowerInvariant();

            if (raw.Contains("application/pdf", StringComparison.Ordinal) ||
                raw.Contains("application/vnd.openxmlformats", StringComparison.Ordinal) ||
                raw.Contains("application/vnd.ms-excel", StringComparison.Ordinal) ||
                raw.Contains("application/octet-stream", StringComparison.Ordinal) ||
                raw.Contains("application/zip", StringComparison.Ordinal))
            {
                return "media_heavy";
            }

            return "media_standard";
        }

        return "media_default";
    }

    /// <summary>
    /// Resolves a compound partition key combining the media type consumption category with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{mediaKey}:{clientKey}"</c>.</returns>
    public static string GetAcceptMediaTypeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var mediaKey = GetAcceptMediaTypeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{mediaKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on HTTP payload compression encoding (<c>"encoding_gzip"</c>, <c>"encoding_brotli"</c>, <c>"encoding_deflate"</c>, <c>"encoding_zstd"</c>, <c>"encoding_identity"</c>).
    /// Defends against Decompression Bomb / Zip Bomb attacks where compressed payloads expand exponentially in memory causing Out-Of-Memory (OOM) failures.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The compression encoding category key.</returns>
    public static string GetCompressionEncodingKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Content-Encoding", out var encVal) && !string.IsNullOrWhiteSpace(encVal))
        {
            var raw = encVal.ToString().ToLowerInvariant();
            if (raw.Contains("gzip", StringComparison.Ordinal))
                return "encoding_gzip";
            if (raw.Contains("br", StringComparison.Ordinal))
                return "encoding_brotli";
            if (raw.Contains("zstd", StringComparison.Ordinal))
                return "encoding_zstd";
            if (raw.Contains("deflate", StringComparison.Ordinal))
                return "encoding_deflate";
        }

        return "encoding_identity";
    }

    /// <summary>
    /// Resolves a compound partition key combining the payload compression encoding with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{encodingKey}:{clientKey}"</c>.</returns>
    public static string GetCompressionEncodingAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var encKey = GetCompressionEncodingKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{encKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on HTTP request header overhead (<c>"hdroverhead_excessive"</c> vs <c>"hdroverhead_normal"</c>).
    /// Defends against Large Header DoS / Cookie Bomb attacks where attackers send hundreds of oversized headers to exhaust gateway parsing CPU.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="maxHeaderCount">Maximum allowed header count before flagging as excessive. Defaults to 50.</param>
    /// <param name="maxTotalBytes">Maximum allowed approximate header bytes before flagging as excessive. Defaults to 8,192 (8 KB).</param>
    /// <returns><c>"hdroverhead_excessive"</c> if thresholds are exceeded; otherwise <c>"hdroverhead_normal"</c>.</returns>
    public static string GetHeaderOverheadBucketKey(HttpContext context, int maxHeaderCount = 50, int maxTotalBytes = 8192)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeaderCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalBytes);

        var headers = context.Request.Headers;
        if (headers.Count > maxHeaderCount)
            return "hdroverhead_excessive";

        var totalBytes = 0;
        foreach (var (k, v) in headers)
        {
            totalBytes += k.Length + (v.ToString()?.Length ?? 0);
            if (totalBytes > maxTotalBytes)
                return "hdroverhead_excessive";
        }

        return "hdroverhead_normal";
    }

    /// <summary>
    /// Resolves a compound partition key combining the request header overhead bucket with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="maxHeaderCount">Maximum allowed header count.</param>
    /// <param name="maxTotalBytes">Maximum allowed total bytes.</param>
    /// <returns>A compound key formatted as <c>"{overheadKey}:{clientKey}"</c>.</returns>
    public static string GetHeaderOverheadBucketAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int maxHeaderCount = 50,
        int maxTotalBytes = 8192)
    {
        ArgumentNullException.ThrowIfNull(context);

        var overheadKey = GetHeaderOverheadBucketKey(context, maxHeaderCount, maxTotalBytes);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{overheadKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on multipart form upload file counts (<c>"files_batch"</c>, <c>"files_single"</c>, <c>"files_none"</c>).
    /// Prevents concurrent multi-file upload streams from overwhelming disk I/O and server memory buffers.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The multipart file count category key.</returns>
    public static string GetMultipartFileCountKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.HasFormContentType && context.Request.Form is { Files.Count: > 0 } form)
        {
            return form.Files.Count > 1 ? "files_batch" : "files_single";
        }

        return "files_none";
    }

    /// <summary>
    /// Resolves a compound partition key combining the multipart file count category with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{fileCountKey}:{clientKey}"</c>.</returns>
    public static string GetMultipartFileCountAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fileCountKey = GetMultipartFileCountKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{fileCountKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on request payload content-type serialization families (<c>"format_xml"</c>, <c>"format_json"</c>, <c>"format_protobuf"</c>, <c>"format_multipart"</c>, <c>"format_form"</c>, <c>"format_empty"</c>, <c>"format_other"</c>).
    /// Isolates CPU-intensive XML parser endpoints (defending against XML Quadratic Blowup and XXE) from lightweight JSON processing.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The content type serialization family key.</returns>
    public static string GetContentTypeFamilyKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ct = context.Request.ContentType;
        if (string.IsNullOrWhiteSpace(ct))
            return "format_empty";

        var raw = ct.ToLowerInvariant();
        if (raw.Contains("xml", StringComparison.Ordinal))
            return "format_xml";
        if (raw.Contains("json", StringComparison.Ordinal))
            return "format_json";
        if (raw.Contains("protobuf", StringComparison.Ordinal) || raw.Contains("x-protobuf", StringComparison.Ordinal))
            return "format_protobuf";
        if (raw.Contains("multipart", StringComparison.Ordinal))
            return "format_multipart";
        if (raw.Contains("form-urlencoded", StringComparison.Ordinal))
            return "format_form";

        return "format_other";
    }

    /// <summary>
    /// Resolves a compound partition key combining the content-type serialization family with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{formatKey}:{clientKey}"</c>.</returns>
    public static string GetContentTypeFamilyAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var formatKey = GetContentTypeFamilyKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{formatKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the best compression algorithm supported in the client's <c>Accept-Encoding</c> header.
    /// Prioritizes high-efficiency algorithms (<c>"encoding_zstd"</c>, <c>"encoding_brotli"</c>, <c>"encoding_gzip"</c>, <c>"encoding_deflate"</c>, <c>"encoding_identity"</c>, <c>"encoding_none"</c>).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The negotiated compression algorithm partition key.</returns>
    public static string GetAcceptEncodingKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Accept-Encoding", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().ToLowerInvariant();
            if (raw.Contains("zstd"))
                return "encoding_zstd";
            if (raw.Contains("br"))
                return "encoding_brotli";
            if (raw.Contains("gzip"))
                return "encoding_gzip";
            if (raw.Contains("deflate"))
                return "encoding_deflate";
            if (raw.Contains("identity"))
                return "encoding_identity";
        }

        return "encoding_none";
    }

    /// <summary>
    /// Resolves a compound partition key combining the best supported compression encoding with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{encodingKey}:{clientKey}"</c>.</returns>
    public static string GetAcceptEncodingAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var encKey = GetAcceptEncodingKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{encKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on HTTP payload digest integrity headers (<c>"digest_present"</c> vs <c>"digest_missing"</c>, RFC 9530 / RFC 3230).
    /// Detects cryptographic payload integrity signatures via <c>"Content-Digest"</c>, <c>"Repr-Digest"</c>, or legacy <c>"Digest"</c> headers.
    /// Segregates tamper-evident authenticated payloads from non-verifiable requests.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"digest_present"</c> if a content digest header is found; otherwise <c>"digest_missing"</c>.</returns>
    public static string GetContentDigestStateKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if ((context.Request.Headers.TryGetValue("Content-Digest", out var cdVal) && !string.IsNullOrWhiteSpace(cdVal)) ||
            (context.Request.Headers.TryGetValue("Repr-Digest", out var rdVal) && !string.IsNullOrWhiteSpace(rdVal)) ||
            (context.Request.Headers.TryGetValue("Digest", out var dVal) && !string.IsNullOrWhiteSpace(dVal)))
        {
            return "digest_present";
        }

        return "digest_missing";
    }

    /// <summary>
    /// Resolves a compound partition key combining the payload digest verification state with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{digestKey}:{clientKey}"</c>.</returns>
    public static string GetContentDigestStateAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var digestKey = GetContentDigestStateKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{digestKey}:{clientKey}";
    }
}
