namespace KyrolusSous.RateLimiting;

/// <summary>
/// HTTP wire protocols, transfer framing, streaming, multiplexing, and priority partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a compound partition key combining the HTTP connection protocol version (e.g. <c>"HTTP/1.1"</c>, <c>"HTTP/2"</c>, <c>"HTTP/3"</c>)
    /// with a client partition key.
    /// Defends against multiplexed stream exhaustion attacks (such as HTTP/2 Rapid Reset CVE-2023-44487) by isolating protocol quotas.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"proto_{protocol}:{clientKey}"</c>.</returns>
    public static string GetProtocolAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var protocol = context.Request.Protocol;
        if (string.IsNullOrWhiteSpace(protocol))
            protocol = "HTTP/1.1";

        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"proto_{protocol.Trim().ToUpperInvariant()}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on persistent streaming connection protocols (<c>"stream_websocket"</c>, <c>"stream_sse"</c>, <c>"stream_grpc"</c>, <c>"stream_unary"</c>).
    /// Enforces strict limits on long-lived TCP connection establishment to prevent socket descriptor and memory buffer exhaustion.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The streaming protocol category key.</returns>
    public static string GetStreamingProtocolKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.WebSockets.IsWebSocketRequest ||
            (context.Request.Headers.TryGetValue("Upgrade", out var upVal) && upVal.ToString().Contains("websocket", StringComparison.OrdinalIgnoreCase)))
        {
            return "stream_websocket";
        }

        if (context.Request.Headers.TryGetValue("Accept", out var acceptVal) &&
            acceptVal.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return "stream_sse";
        }

        if (context.Request.ContentType is not null &&
            context.Request.ContentType.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase))
        {
            return "stream_grpc";
        }

        return "stream_unary";
    }

    /// <summary>
    /// Resolves a compound partition key combining the streaming protocol category with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{streamingKey}:{clientKey}"</c>.</returns>
    public static string GetStreamingProtocolAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var streamKey = GetStreamingProtocolKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{streamKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on HTTP Range requests (<c>"range_chunked"</c> vs <c>"range_full"</c>).
    /// Throttles rapid byte-range chunk requests to defend against file I/O seek exhaustion attacks on streaming/download endpoints.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"range_chunked"</c> if the request includes a <c>Range</c> header; otherwise <c>"range_full"</c>.</returns>
    public static string GetRangeRequestKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Headers.ContainsKey("Range") ? "range_chunked" : "range_full";
    }

    /// <summary>
    /// Resolves a compound partition key combining the range request category with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{rangeKey}:{clientKey}"</c>.</returns>
    public static string GetRangeRequestAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rangeKey = GetRangeRequestKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{rangeKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the HTTP Extensible Priority header (RFC 9218) used in HTTP/2 and HTTP/3 multiplexing.
    /// Extracts the urgency level (<c>u=0</c> for critical down to <c>u=7</c> for background) to guarantee capacity for high-priority interactive requests.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The priority partition key formatted as <c>"priority_u{n}"</c>, or <c>"priority_default"</c>.</returns>
    public static string GetHttpPriorityKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Priority", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString();
            var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (token.StartsWith("u=", StringComparison.OrdinalIgnoreCase) &&
                    token.Length >= 3 &&
                    char.IsDigit(token[2]))
                {
                    var urgency = token[2];
                    return $"priority_u{urgency}";
                }
            }
        }

        return "priority_default";
    }

    /// <summary>
    /// Resolves a compound partition key combining the HTTP Extensible Priority urgency with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{priorityKey}:{clientKey}"</c>.</returns>
    public static string GetHttpPriorityAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var prioKey = GetHttpPriorityKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{prioKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the negotiated WebSocket subprotocol via <c>Sec-WebSocket-Protocol</c> (RFC 6455).
    /// Segregates specialized real-time protocols (e.g. <c>"graphql-transport-ws"</c>, <c>"wamp.2.json"</c>, <c>"mqtt"</c>) from generic raw streams.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The WebSocket subprotocol key formatted as <c>"ws_subproto_{name}"</c>, or <c>"ws_subproto_none"</c>.</returns>
    public static string GetWebSocketSubprotocolKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Sec-WebSocket-Protocol", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString();
            var commaIdx = raw.IndexOf(',');
            var proto = commaIdx >= 0 ? raw[..commaIdx].Trim() : raw.Trim();
            if (!string.IsNullOrWhiteSpace(proto))
                return $"ws_subproto_{proto.ToLowerInvariant()}";
        }

        return "ws_subproto_none";
    }

    /// <summary>
    /// Resolves a compound partition key combining the WebSocket subprotocol with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{wsSubprotoKey}:{clientKey}"</c>.</returns>
    public static string GetWebSocketSubprotocolAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var wsKey = GetWebSocketSubprotocolKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{wsKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the HTTP wire protocol version (<c>"proto_http3"</c>, <c>"proto_http2"</c>, <c>"proto_http11"</c>, <c>"proto_http10"</c>, <c>"proto_unknown"</c>).
    /// Encourages multiplexed modern protocols (HTTP/2 and HTTP/3) with higher concurrency budgets while restricting TCP-exhausting HTTP/1.1 connections.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The HTTP protocol partition key.</returns>
    public static string GetHttpProtocolKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var protocol = context.Request.Protocol;
        if (string.IsNullOrWhiteSpace(protocol))
            return "proto_unknown";

        if (HttpProtocol.IsHttp3(protocol))
            return "proto_http3";
        if (HttpProtocol.IsHttp2(protocol))
            return "proto_http2";
        if (HttpProtocol.IsHttp11(protocol))
            return "proto_http11";
        if (HttpProtocol.IsHttp10(protocol))
            return "proto_http10";

        return $"proto_{protocol.Trim().ToLowerInvariant().Replace('/', '_').Replace('.', '_')}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the HTTP wire protocol version with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{protoKey}:{clientKey}"</c>.</returns>
    public static string GetHttpProtocolAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var protoKey = GetHttpProtocolKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{protoKey}:{clientKey}";
    }

    // =========================================================================
    // 10. PROTOCOL INTEGRITY, CDN EDGE POPS & ZERO-TRUST SECURITY SIGNALS
    // =========================================================================

    /// <summary>
    /// Resolves a partition key based on the HTTP request body transfer framing (<c>"transfer_chunked"</c> vs <c>"transfer_identity"</c>).
    /// Enforces strict concurrent connection quotas on chunked requests to shield backends from HTTP Request Smuggling (CL.TE / TE.CL) and Slowloris attacks.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"transfer_chunked"</c> if chunked transfer is active; otherwise <c>"transfer_identity"</c>.</returns>
    public static string GetTransferEncodingKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Transfer-Encoding", out var teVal) && !string.IsNullOrWhiteSpace(teVal))
        {
            if (teVal.ToString().Contains("chunked", StringComparison.OrdinalIgnoreCase))
                return "transfer_chunked";
        }

        return "transfer_identity";
    }

    /// <summary>
    /// Resolves a compound partition key combining the transfer encoding with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{transferKey}:{clientKey}"</c>.</returns>
    public static string GetTransferEncodingAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var teKey = GetTransferEncodingKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{teKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on HTTP connection persistence management (<c>"conn_mode_close"</c>, <c>"conn_mode_upgrade"</c>, or <c>"conn_mode_keepalive"</c>).
    /// Defends against socket TIME_WAIT exhaustion where attackers repeatedly open and immediately close connections to exhaust ephemeral ports.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The connection persistence mode partition key.</returns>
    public static string GetConnectionHeaderKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Connection", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().ToLowerInvariant();
            if (raw.Contains("close"))
                return "conn_mode_close";
            if (raw.Contains("upgrade"))
                return "conn_mode_upgrade";
        }

        return "conn_mode_keepalive";
    }

    /// <summary>
    /// Resolves a compound partition key combining the connection mode with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{connModeKey}:{clientKey}"</c>.</returns>
    public static string GetConnectionHeaderAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var connKey = GetConnectionHeaderKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{connKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key categorizing multiplexed HTTP/2 and HTTP/3 stream concurrency depth (<c>"stream_depth_low"</c>, <c>"stream_depth_moderate"</c>, <c>"stream_depth_saturated"</c>).
    /// Detects stream count from header <c>"X-Concurrent-Streams"</c> or query parameter <c>"streams"</c>.
    /// Mitigates HTTP/2 Rapid Reset and stream multiplexing denial-of-service vectors (CVE-2023-44487).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="moderateThreshold">Upper stream count threshold for low concurrency. Must be &gt; 0. Default is 10.</param>
    /// <param name="saturatedThreshold">Stream count threshold above which concurrency is saturated. Must be &gt; <paramref name="moderateThreshold"/>. Default is 50.</param>
    /// <returns>The stream concurrency bucket partition key.</returns>
    public static string GetConcurrentStreamBucketKey(HttpContext context, int moderateThreshold = 10, int saturatedThreshold = 50)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(moderateThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(saturatedThreshold, moderateThreshold);

        int streams = 1;
        if (context.Request.Headers.TryGetValue("X-Concurrent-Streams", out var hVal) &&
            int.TryParse(hVal, out var parsedH))
        {
            streams = parsedH;
        }
        else if (context.Request.Query.TryGetValue("streams", out var qVal) &&
                 int.TryParse(qVal, out var parsedQ))
        {
            streams = parsedQ;
        }

        if (streams <= moderateThreshold)
            return "stream_depth_low";

        if (streams <= saturatedThreshold)
            return "stream_depth_moderate";

        return "stream_depth_saturated";
    }

    /// <summary>
    /// Resolves a compound partition key combining the multiplexed stream concurrency bucket with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="moderateThreshold">Upper stream count threshold for low concurrency. Must be &gt; 0. Default is 10.</param>
    /// <param name="saturatedThreshold">Stream count threshold above which concurrency is saturated. Must be &gt; <paramref name="moderateThreshold"/>. Default is 50.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{streamBucketKey}:{clientKey}"</c>.</returns>
    public static string GetConcurrentStreamBucketAndClientKey(
        HttpContext context,
        int moderateThreshold = 10,
        int saturatedThreshold = 50,
        Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bucketKey = GetConcurrentStreamBucketKey(context, moderateThreshold, saturatedThreshold);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{bucketKey}:{clientKey}";
    }
}
