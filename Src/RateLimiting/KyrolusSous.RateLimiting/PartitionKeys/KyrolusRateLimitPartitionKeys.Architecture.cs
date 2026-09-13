namespace KyrolusSous.RateLimiting;

/// <summary>
/// Distributed tracing, hyperscale sharding, webhook storms, and load-shedding architecture partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Dynamically constructs an arbitrary composite partition key by executing a series of resolvers in sequence and joining their results with a delimiter.
    /// Provides maximum architectural flexibility for multi-dimensional rate limiting (e.g. <c>Tenant + Role + Route</c> or <c>Host + Subnet</c>).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="delimiter">The delimiter string used to separate key components (defaults to <c>":"</c>).</param>
    /// <param name="resolvers">The sequence of resolver functions to execute.</param>
    /// <returns>The combined compound key string.</returns>
    public static string GetCompositeKey(HttpContext context, string delimiter = ":", params Func<HttpContext, string>[] resolvers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(delimiter);
        ArgumentNullException.ThrowIfNull(resolvers);

        if (resolvers.Length == 0)
            return GetClientIpKey(context);

        if (resolvers.Length == 1)
            return resolvers[0](context);

        var parts = new string[resolvers.Length];
        for (var i = 0; i < resolvers.Length; i++)
        {
            parts[i] = resolvers[i](context);
        }

        return string.Join(delimiter, parts);
    }

    /// <summary>
    /// Resolves a partition key based on time-of-day business hours (<c>"window_peak"</c> vs <c>"window_offpeak"</c>),
    /// allowing generous batch synchronization quotas during off-peak night hours while enforcing strict SLAs during peak business hours.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="peakStartHour">Peak hour start (0-23, inclusive). Defaults to 8 (08:00).</param>
    /// <param name="peakEndHour">Peak hour end (0-23, exclusive). Defaults to 18 (18:00).</param>
    /// <param name="timeProvider">Optional TimeProvider for testability. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns><c>"window_peak"</c> during peak business hours; otherwise <c>"window_offpeak"</c>.</returns>
    public static string GetTimeWindowKey(HttpContext context, int peakStartHour = 8, int peakEndHour = 18, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(peakStartHour);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(peakStartHour, 23);
        ArgumentOutOfRangeException.ThrowIfNegative(peakEndHour);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(peakEndHour, 24);

        var provider = timeProvider ?? TimeProvider.System;
        var currentHour = provider.GetUtcNow().Hour;

        var isPeak = peakStartHour <= peakEndHour
            ? currentHour >= peakStartHour && currentHour < peakEndHour
            : currentHour >= peakStartHour || currentHour < peakEndHour;

        return isPeak ? "window_peak" : "window_offpeak";
    }

    /// <summary>
    /// Resolves a compound partition key combining the time window (<c>"window_peak"</c> / <c>"window_offpeak"</c>) with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="peakStartHour">Peak hour start (0-23, inclusive). Defaults to 8.</param>
    /// <param name="peakEndHour">Peak hour end (0-23, exclusive). Defaults to 18.</param>
    /// <param name="timeProvider">Optional TimeProvider for testability.</param>
    /// <returns>A compound key formatted as <c>"{windowKey}:{clientKey}"</c>.</returns>
    public static string GetTimeWindowAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int peakStartHour = 8,
        int peakEndHour = 18,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var windowKey = GetTimeWindowKey(context, peakStartHour, peakEndHour, timeProvider);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{windowKey}:{clientKey}";
    }

    private static readonly string[] DefaultCostHeaders =
    [
        "X-Query-Complexity", // GraphQL query complexity
        "X-Request-Cost",      // Custom business logic cost
        "X-Batch-Size"         // Bulk import/batch count
    ];

    /// <summary>
    /// Resolves a partition key based on a declared query complexity or batch cost header (<c>"cost_heavy"</c>, <c>"cost_medium"</c>, <c>"cost_standard"</c>),
    /// ensuring GraphQL deep-nested AST queries and large batch requests are throttled proportionally to their server computational weight.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="costHeaderCandidates">Optional prioritized list of header names to search for cost/complexity.</param>
    /// <param name="heavyThreshold">Threshold above which a request is marked as heavy. Defaults to 100.</param>
    /// <param name="mediumThreshold">Threshold above which a request is marked as medium. Defaults to 20.</param>
    /// <returns>The cost category partition key.</returns>
    public static string GetRequestCostKey(
        HttpContext context,
        string[]? costHeaderCandidates = null,
        int heavyThreshold = 100,
        int mediumThreshold = 20)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(mediumThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(heavyThreshold, mediumThreshold);

        var candidates = costHeaderCandidates is { Length: > 0 } ? costHeaderCandidates : DefaultCostHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();
                if (int.TryParse(raw, CultureInfo.InvariantCulture, out var cost))
                {
                    if (cost >= heavyThreshold)
                        return "cost_heavy";
                    if (cost >= mediumThreshold)
                        return "cost_medium";
                    return "cost_standard";
                }
            }
        }

        return "cost_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the request cost classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="costHeaderCandidates">Optional prioritized list of header names to search for cost/complexity.</param>
    /// <param name="heavyThreshold">Threshold above which a request is marked as heavy. Defaults to 100.</param>
    /// <param name="mediumThreshold">Threshold above which a request is marked as medium. Defaults to 20.</param>
    /// <returns>A compound key formatted as <c>"{costKey}:{clientKey}"</c>.</returns>
    public static string GetRequestCostAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? costHeaderCandidates = null,
        int heavyThreshold = 100,
        int mediumThreshold = 20)
    {
        ArgumentNullException.ThrowIfNull(context);

        var costKey = GetRequestCostKey(context, costHeaderCandidates, heavyThreshold, mediumThreshold);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{costKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the request caching policy (<c>"cache_bypass"</c> vs <c>"cache_standard"</c>).
    /// Detects and heavily throttles Cache-Busting DoS attacks where attackers send <c>Cache-Control: no-cache</c>,
    /// <c>Pragma: no-cache</c>, or timestamp cache-busting query parameters to force expensive upstream origin hits.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"cache_bypass"</c> if request attempts to bypass caching; otherwise <c>"cache_standard"</c>.</returns>
    public static string GetCachePolicyKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Cache-Control", out var ccVal))
        {
            var cc = ccVal.ToString();
            if (cc.Contains("no-cache", StringComparison.OrdinalIgnoreCase) ||
                cc.Contains("no-store", StringComparison.OrdinalIgnoreCase) ||
                cc.Contains("max-age=0", StringComparison.OrdinalIgnoreCase))
            {
                return "cache_bypass";
            }
        }

        if (context.Request.Headers.TryGetValue("Pragma", out var pragmaVal) &&
            pragmaVal.ToString().Contains("no-cache", StringComparison.OrdinalIgnoreCase))
        {
            return "cache_bypass";
        }

        var query = context.Request.Query;
        if (query.ContainsKey("_nocache") || query.ContainsKey("_") || query.ContainsKey("nocache"))
        {
            return "cache_bypass";
        }

        return "cache_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the request caching policy with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{cachePolicyKey}:{clientKey}"</c>.</returns>
    public static string GetCachePolicyAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cacheKey = GetCachePolicyKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{cacheKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the request priority or Quality of Service (QoS) tier (<c>"priority_critical"</c>, <c>"priority_background"</c>, <c>"priority_standard"</c>).
    /// Prevents non-essential background telemetry, metrics, and batch requests from crowding out high-priority user transactions.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The priority tier partition key.</returns>
    public static string GetPriorityKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("X-Priority", out var prioVal) ||
            context.Request.Headers.TryGetValue("Priority", out prioVal))
        {
            var raw = prioVal.ToString().Trim().ToLowerInvariant();
            if (raw is "high" or "critical" or "urgent" or "1" or "u=1" or "u=0")
                return "priority_critical";
            if (raw is "low" or "background" or "idle" or "u=5" or "u=6" or "u=7")
                return "priority_background";
        }

        return "priority_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the request priority category with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{priorityKey}:{clientKey}"</c>.</returns>
    public static string GetPriorityAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var prioKey = GetPriorityKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{prioKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the distributed transaction correlation identifier (extracted from W3C <c>traceparent</c> or <c>X-Correlation-ID</c>).
    /// Enforces a ceiling on downstream request amplification to stop recursive microservice fan-out loops and storm cascades.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The correlation partition key formatted as <c>"trace_{traceId}"</c>, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetCorrelationKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("traceparent", out var tpVal) && !string.IsNullOrWhiteSpace(tpVal))
        {
            var raw = tpVal.ToString().Trim();
            var parts = raw.Split('-');
            if (parts.Length >= 4 && parts[1].Length == 32)
                return $"trace_{parts[1].ToLowerInvariant()}";
        }

        if ((context.Request.Headers.TryGetValue("X-Correlation-ID", out var corrVal) && !string.IsNullOrWhiteSpace(corrVal)) ||
            (context.Request.Headers.TryGetValue("X-Request-ID", out corrVal) && !string.IsNullOrWhiteSpace(corrVal)))
        {
            var raw = corrVal.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw))
                return $"trace_{raw.ToLowerInvariant()}";
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the transaction correlation identifier with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{correlationKey}:{clientKey}"</c>.</returns>
    public static string GetCorrelationAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var corrKey = GetCorrelationKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{corrKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the gateway's adaptive load-shedding operating state (<c>"state_degraded"</c> vs <c>"state_normal"</c>).
    /// Automatically switches client partitioning during incidents or high saturation to shed non-essential load while preserving critical path traffic.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="stateHeaderName">The header name signaling load-shedding state. Defaults to <c>"X-System-Load"</c>.</param>
    /// <returns><c>"state_degraded"</c> if system load is elevated or shedding; otherwise <c>"state_normal"</c>.</returns>
    public static string GetLoadSheddingStateKey(HttpContext context, string stateHeaderName = "X-System-Load")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateHeaderName);

        if (context.Request.Headers.TryGetValue(stateHeaderName, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (raw is "degraded" or "overloaded" or "shedding" or "high" or "critical")
                return "state_degraded";
        }

        if (context.Items.TryGetValue(stateHeaderName, out var itemVal) && itemVal is not null)
        {
            var raw = itemVal.ToString()?.Trim().ToLowerInvariant();
            if (raw is "degraded" or "overloaded" or "shedding" or "high" or "critical")
                return "state_degraded";
        }

        return "state_normal";
    }

    /// <summary>
    /// Resolves a compound partition key combining the adaptive load-shedding state with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="stateHeaderName">The header name signaling load-shedding state.</param>
    /// <returns>A compound key formatted as <c>"{loadSheddingStateKey}:{clientKey}"</c>.</returns>
    public static string GetLoadSheddingStateAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string stateHeaderName = "X-System-Load")
    {
        ArgumentNullException.ThrowIfNull(context);

        var stateKey = GetLoadSheddingStateKey(context, stateHeaderName);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{stateKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on Shuffle Sharding virtual cell assignment (<c>"cell_{shardIndex}"</c>).
    /// Implements the AWS Route 53 / Cellular fault-isolation pattern, partitioning clients deterministically into N isolated cells
    /// so that a noisy neighbor or DDoS attack on one cell cannot affect the remaining shards.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="totalShards">The total number of fault-isolated virtual shards (e.g. 16, 32, 64). Defaults to 16.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity used for the deterministic hash.</param>
    /// <returns>The virtual shard cell partition key formatted as <c>"cell_{index}"</c>.</returns>
    public static string GetShardedCellKey(
        HttpContext context,
        int totalShards = 16,
        Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalShards);

        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        var hash = (uint)string.GetHashCode(clientKey, StringComparison.Ordinal);
        var cellIndex = (int)(hash % (uint)totalShards);

        return $"cell_{cellIndex}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the Shuffle Sharding cell assignment with the client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="totalShards">The total number of fault-isolated virtual shards.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity.</param>
    /// <returns>A compound key formatted as <c>"{cellKey}:{clientKey}"</c>.</returns>
    public static string GetShardedCellAndClientKey(
        HttpContext context,
        int totalShards = 16,
        Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        var cellKey = GetShardedCellKey(context, totalShards, _ => clientKey);
        return $"{cellKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the named GraphQL operation (extracted from <c>"X-GraphQL-Operation-Name"</c> header or <c>"operationName"</c> query parameter).
    /// Enables granular rate limiting for individual GraphQL queries and mutations on a single unified <c>/graphql</c> endpoint.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The GraphQL operation partition key formatted as <c>"gql_{operationName}"</c>, or <c>"gql_anonymous"</c>.</returns>
    public static string GetGraphQlOperationKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if ((context.Request.Headers.TryGetValue("X-GraphQL-Operation-Name", out var opVal) && !string.IsNullOrWhiteSpace(opVal)) ||
            (context.Request.Headers.TryGetValue("GraphQL-Operation-Name", out opVal) && !string.IsNullOrWhiteSpace(opVal)) ||
            (context.Request.Query.TryGetValue("operationName", out opVal) && !string.IsNullOrWhiteSpace(opVal)))
        {
            var raw = opVal.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw))
                return $"gql_{raw}";
        }

        return "gql_anonymous";
    }

    /// <summary>
    /// Resolves a compound partition key combining the named GraphQL operation with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{graphQlKey}:{clientKey}"</c>.</returns>
    public static string GetGraphQlOperationAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var gqlKey = GetGraphQlOperationKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{gqlKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the client User-Agent software engine category (<c>"engine_automated"</c>, <c>"engine_crawler"</c>, <c>"engine_webview"</c>, <c>"engine_browser"</c>, <c>"engine_unknown"</c>).
    /// Enforces tiered rate limit policies isolating automated scripting tools (e.g. curl, python-requests, postman) from genuine end-user browsers.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The User-Agent software engine category key.</returns>
    public static string GetUserAgentEngineKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("User-Agent", out var uaVal) && !string.IsNullOrWhiteSpace(uaVal))
        {
            var ua = uaVal.ToString().ToLowerInvariant();

            if (ua.Contains("curl", StringComparison.Ordinal) ||
                ua.Contains("python", StringComparison.Ordinal) ||
                ua.Contains("postman", StringComparison.Ordinal) ||
                ua.Contains("httpclient", StringComparison.Ordinal) ||
                ua.Contains("wget", StringComparison.Ordinal) ||
                ua.Contains("axios", StringComparison.Ordinal))
            {
                return "engine_automated";
            }

            if (ua.Contains("googlebot", StringComparison.Ordinal) ||
                ua.Contains("bingbot", StringComparison.Ordinal) ||
                ua.Contains("slurp", StringComparison.Ordinal) ||
                ua.Contains("duckduckbot", StringComparison.Ordinal) ||
                ua.Contains("baiduspider", StringComparison.Ordinal) ||
                ua.Contains("yandexbot", StringComparison.Ordinal))
            {
                return "engine_crawler";
            }

            if (ua.Contains("; wv", StringComparison.Ordinal) || ua.Contains("webview", StringComparison.Ordinal))
                return "engine_webview";

            if (ua.Contains("mozilla", StringComparison.Ordinal) ||
                ua.Contains("chrome", StringComparison.Ordinal) ||
                ua.Contains("safari", StringComparison.Ordinal) ||
                ua.Contains("firefox", StringComparison.Ordinal) ||
                ua.Contains("edge", StringComparison.Ordinal))
            {
                return "engine_browser";
            }

            return "engine_other";
        }

        return "engine_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the User-Agent engine category with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{engineKey}:{clientKey}"</c>.</returns>
    public static string GetUserAgentEngineAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var engineKey = GetUserAgentEngineKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{engineKey}:{clientKey}";
    }

    private static readonly string[] DefaultWebhookTargetHeaders =
    [
        "X-Webhook-Target",
        "X-Callback-URL",
        "Webhook-URL"
    ];

    /// <summary>
    /// Resolves a partition key based on the destination hostname of an outbound or routed webhook callback.
    /// Prevents downstream partner server slowdowns from jamming the gateway's outbound dispatcher queues.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="webhookHeaderCandidates">Optional prioritized list of header names to search for callback target.</param>
    /// <returns>The webhook target domain key formatted as <c>"webhook_{domain}"</c>, or <c>"webhook_unknown"</c>.</returns>
    public static string GetWebhookTargetDomainKey(HttpContext context, string[]? webhookHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = webhookHeaderCandidates is { Length: > 0 } ? webhookHeaderCandidates : DefaultWebhookTargetHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();
                if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                    return $"webhook_{uri.Host.ToLowerInvariant()}";

                return $"webhook_{raw.ToLowerInvariant()}";
            }
        }

        return "webhook_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the destination webhook hostname with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="webhookHeaderCandidates">Optional prioritized list of header names to search for callback target.</param>
    /// <returns>A compound key formatted as <c>"{webhookKey}:{clientKey}"</c>.</returns>
    public static string GetWebhookTargetDomainAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? webhookHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var whKey = GetWebhookTargetDomainKey(context, webhookHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{whKey}:{clientKey}";
    }

    // =========================================================================
    // 8. HYPERSCALE PROTOCOL, W3C BAGGAGE & ZERO-TRUST EDGE SIGNALS
    // =========================================================================

    /// <summary>
    /// Resolves a partition key based on a specific attribute extracted from the W3C Distributed Tracing <c>baggage</c> header (RFC OpenTelemetry).
    /// Enables distributed context propagation where upstream microservices or edge gateways inject tenant tier or business segmentation metadata.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="baggageKey">The specific baggage attribute key name to extract (e.g. <c>"tier"</c>, <c>"experiment"</c>).</param>
    /// <returns>The baggage partition key formatted as <c>"baggage_{key}_{value}"</c>, or <c>"baggage_{key}_missing"</c>.</returns>
    public static string GetW3cBaggageKey(HttpContext context, string baggageKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(baggageKey);

        var keyLower = baggageKey.Trim().ToLowerInvariant();
        if (context.Request.Headers.TryGetValue("baggage", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString();
            var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                var semicolonIdx = token.IndexOf(';');
                var pair = semicolonIdx >= 0 ? token[..semicolonIdx].Trim() : token.Trim();
                var equalsIdx = pair.IndexOf('=');
                if (equalsIdx > 0)
                {
                    var k = pair[..equalsIdx].Trim();
                    if (string.Equals(k, keyLower, StringComparison.OrdinalIgnoreCase))
                    {
                        var v = pair[(equalsIdx + 1)..].Trim();
                        if (!string.IsNullOrWhiteSpace(v))
                            return $"baggage_{keyLower}_{v.ToLowerInvariant()}";
                    }
                }
            }
        }

        return $"baggage_{keyLower}_missing";
    }

    /// <summary>
    /// Resolves a compound partition key combining a W3C baggage attribute with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="baggageKey">The specific baggage attribute key name.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{baggageKey}:{clientKey}"</c>.</returns>
    public static string GetW3cBaggageAndClientKey(
        HttpContext context,
        string baggageKey,
        Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bagKey = GetW3cBaggageKey(context, baggageKey);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{bagKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the W3C Fetch Metadata <c>Sec-Fetch-Site</c> header (<c>"site_same-origin"</c>, <c>"site_same-site"</c>, <c>"site_cross-site"</c>, <c>"site_none"</c>).
    /// Enables strong isolation against Cross-Site Request Forgery (CSRF) and cross-site resource exhaustion attacks by strictly rationing cross-site requests.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The Fetch Site partition key formatted as <c>"site_{value}"</c>, or <c>"site_unknown"</c>.</returns>
    public static string GetSecFetchSiteKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Sec-Fetch-Site", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(raw))
                return $"site_{raw}";
        }

        return "site_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the W3C Sec-Fetch-Site classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{fetchSiteKey}:{clientKey}"</c>.</returns>
    public static string GetSecFetchSiteAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var siteKey = GetSecFetchSiteKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{siteKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the W3C Fetch Metadata <c>Sec-Fetch-Dest</c> header (<c>"dest_document"</c>, <c>"dest_empty"</c>, <c>"dest_image"</c>, <c>"dest_script"</c>, <c>"dest_style"</c>).
    /// Allows the gateway to allocate distinct rate limit pools for browser navigation (<c>"dest_document"</c>) vs API fetch/XHR calls (<c>"dest_empty"</c>) vs media asset requests.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The Fetch Destination partition key formatted as <c>"dest_{value}"</c>, or <c>"dest_unknown"</c>.</returns>
    public static string GetSecFetchDestKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Sec-Fetch-Dest", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(raw))
                return $"dest_{raw}";
        }

        return "dest_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the W3C Sec-Fetch-Dest classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{fetchDestKey}:{clientKey}"</c>.</returns>
    public static string GetSecFetchDestAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var destKey = GetSecFetchDestKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{destKey}:{clientKey}";
    }

    // =========================================================================
    // 9. HTTP SEMANTICS, ZERO-DB API TIERS & HARDWARE CLIENT SIGNALS
    // =========================================================================

    /// <summary>
    /// Resolves a partition key based on the W3C Fetch Metadata <c>Sec-Fetch-Mode</c> header (<c>"fetch_mode_cors"</c>, <c>"fetch_mode_no-cors"</c>, <c>"fetch_mode_navigate"</c>, <c>"fetch_mode_same-origin"</c>, <c>"fetch_mode_websocket"</c>).
    /// Prevents CSRF and cross-origin smuggling attacks by strictly throttling opaque <c>"no-cors"</c> requests.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The Fetch Mode partition key formatted as <c>"fetch_mode_{mode}"</c>, or <c>"fetch_mode_unknown"</c>.</returns>
    public static string GetSecFetchModeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Sec-Fetch-Mode", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(raw))
                return $"fetch_mode_{raw}";
        }

        return "fetch_mode_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the W3C Sec-Fetch-Mode classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{fetchModeKey}:{clientKey}"</c>.</returns>
    public static string GetSecFetchModeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modeKey = GetSecFetchModeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{modeKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on conditional HTTP headers (RFC 9110 Conditional Requests).
    /// Categorizes requests into <c>"cond_etag"</c> (If-None-Match), <c>"cond_concurrency"</c> (If-Match), <c>"cond_time"</c> (If-Modified-Since / If-Unmodified-Since), <c>"cond_range"</c> (If-Range), or <c>"cond_none"</c>.
    /// Isolates cache validation stampedes (Thundering Herd / 304 storms) from standard uncached requests.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The conditional request partition key.</returns>
    public static string GetConditionalRequestTypeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Request.Headers;
        if (headers.ContainsKey("If-None-Match"))
            return "cond_etag";
        if (headers.ContainsKey("If-Match"))
            return "cond_concurrency";
        if (headers.ContainsKey("If-Modified-Since") || headers.ContainsKey("If-Unmodified-Since"))
            return "cond_time";
        if (headers.ContainsKey("If-Range"))
            return "cond_range";

        return "cond_none";
    }

    /// <summary>
    /// Resolves a compound partition key combining the conditional request type with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{conditionalKey}:{clientKey}"</c>.</returns>
    public static string GetConditionalRequestTypeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var condKey = GetConditionalRequestTypeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{condKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on client execution preference (<c>"exec_async"</c> vs <c>"exec_sync"</c>, RFC 7240).
    /// Detects requests opting into asynchronous processing via <c>"Prefer: respond-async"</c>.
    /// Protects synchronous threadpools from starvation while allowing high concurrency for non-blocking asynchronous jobs.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"exec_async"</c> if asynchronous execution was preferred; otherwise <c>"exec_sync"</c>.</returns>
    public static string GetAsyncExecutionModeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Prefer", out var preferVal) && !string.IsNullOrWhiteSpace(preferVal))
        {
            if (preferVal.ToString().Contains("respond-async", StringComparison.OrdinalIgnoreCase))
                return "exec_async";
        }

        return "exec_sync";
    }

    /// <summary>
    /// Resolves a compound partition key combining the execution mode with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{execModeKey}:{clientKey}"</c>.</returns>
    public static string GetAsyncExecutionModeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var execModeKey = GetAsyncExecutionModeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{execModeKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the origin or target cloud region (<c>"X-Region"</c>, <c>"X-Azure-Origin-Region"</c>, <c>"X-AWS-Region"</c>, <c>"X-Edge-Region"</c>).
    /// Enables geo-fenced failover and localized rate limiting partitions across multi-region deployments.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The region partition key formatted as <c>"region_{region}"</c>, or <c>"region_default"</c>.</returns>
    public static string GetOriginRegionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? region = null;
        if (context.Request.Headers.TryGetValue("X-Region", out var rVal) && !string.IsNullOrWhiteSpace(rVal))
            region = rVal.ToString();
        else if (context.Request.Headers.TryGetValue("X-Azure-Origin-Region", out var azVal) && !string.IsNullOrWhiteSpace(azVal))
            region = azVal.ToString();
        else if (context.Request.Headers.TryGetValue("X-AWS-Region", out var awsVal) && !string.IsNullOrWhiteSpace(awsVal))
            region = awsVal.ToString();
        else if (context.Request.Headers.TryGetValue("X-Edge-Region", out var edgeVal) && !string.IsNullOrWhiteSpace(edgeVal))
            region = edgeVal.ToString();

        if (string.IsNullOrWhiteSpace(region))
            return "region_default";

        var sanitized = region.Trim().ToLowerInvariant().Replace(':', '_').Replace('/', '_').Replace(' ', '_');
        return $"region_{sanitized}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the cloud region key with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{regionKey}:{clientKey}"</c>.</returns>
    public static string GetOriginRegionAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var regionKey = GetOriginRegionKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{regionKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on webhook delivery attempt counters (<c>"attempt_initial"</c>, <c>"attempt_retry_low"</c>, <c>"attempt_retry_high"</c>).
    /// Inspects headers from providers like Stripe (<c>"Stripe-Delivery-Attempt"</c>), GitHub (<c>"X-GitHub-Delivery-Attempt"</c>), or standard (<c>"X-Delivery-Attempt"</c>, <c>"X-Retry-Count"</c>).
    /// Throttles downstream thundering-herd webhook storms when third parties retry simultaneously following transient outages.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The webhook attempt partition key.</returns>
    public static string GetWebhookAttemptCountKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        int attempt = 1;
        if (context.Request.Headers.TryGetValue("Stripe-Delivery-Attempt", out var sVal) && int.TryParse(sVal, out var sParsed))
            attempt = sParsed;
        else if (context.Request.Headers.TryGetValue("X-GitHub-Delivery-Attempt", out var gVal) && int.TryParse(gVal, out var gParsed))
            attempt = gParsed;
        else if (context.Request.Headers.TryGetValue("X-Delivery-Attempt", out var dVal) && int.TryParse(dVal, out var dParsed))
            attempt = dParsed;
        else if (context.Request.Headers.TryGetValue("X-Retry-Count", out var rVal) && int.TryParse(rVal, out var rParsed))
            attempt = rParsed + 1;

        if (attempt <= 1)
            return "attempt_initial";
        if (attempt <= 5)
            return "attempt_retry_low";

        return "attempt_retry_high";
    }

    /// <summary>
    /// Resolves a compound partition key combining the webhook attempt count bucket with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{attemptKey}:{clientKey}"</c>.</returns>
    public static string GetWebhookAttemptCountAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attemptKey = GetWebhookAttemptCountKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{attemptKey}:{clientKey}";
    }
}
