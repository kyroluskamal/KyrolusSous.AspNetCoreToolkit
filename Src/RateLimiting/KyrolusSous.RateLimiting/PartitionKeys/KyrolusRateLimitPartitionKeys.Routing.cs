using KyrolusSous.RateLimiting.Abstractions;

namespace KyrolusSous.RateLimiting;

/// <summary>
/// HTTP routes, methods, queries, headers, hostnames, and endpoint SLA lifecycle partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a partition key based on a custom client request header (e.g. <c>"X-Device-ID"</c>, <c>"CF-IPCountry"</c>, or <c>"X-Session-ID"</c>),
    /// falling back to the client IP address if the header is absent or empty.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="headerName">The custom request header name to inspect.</param>
    /// <param name="prefix">Optional prefix for the partition key. If null, the header name in lowercase is used.</param>
    /// <returns>The header partition key, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetCustomHeaderKey(HttpContext context, string headerName, string? prefix = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        if (context.Request.Headers.TryGetValue(headerName, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var p = prefix ?? headerName.Trim().ToLowerInvariant();
                return $"{p}_{raw}";
            }
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the route endpoint identifier with a client partition key.
    /// Prevents cross-endpoint quota starvation by isolating rate limits per route per client.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity partition key. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{route}:{clientKey}"</c>.</returns>
    public static string GetRouteAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.GetEndpoint();
        var routeIdentifier = endpoint?.DisplayName
                           ?? context.Request.Path.Value
                           ?? "unknown_route";

        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{routeIdentifier}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on an HTTP session or client cookie (defaults to <c>".AspNetCore.Session"</c>).
    /// Prevents shared-IP starvation in corporate NAT environments by isolating individual browser sessions.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="cookieName">The cookie name to inspect. Defaults to <c>".AspNetCore.Session"</c>.</param>
    /// <returns>The cookie partition key formatted as <c>"cookie_{value}"</c>, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetSessionCookieKey(HttpContext context, string cookieName = ".AspNetCore.Session")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieName);

        if (context.Request.Cookies.TryGetValue(cookieName, out var cookieVal) && !string.IsNullOrWhiteSpace(cookieVal))
        {
            var raw = cookieVal.Trim();
            if (!string.IsNullOrWhiteSpace(raw))
                return $"cookie_{raw}";
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the HTTP request method (e.g. <c>"GET"</c>, <c>"POST"</c>) with a client partition key.
    /// Enables separate throttling quotas for heavy write mutations vs lightweight read queries.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity partition key. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{method}:{clientKey}"</c>.</returns>
    public static string GetMethodAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var method = context.Request.Method.ToUpperInvariant();
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{method}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on a query string parameter (e.g. <c>"?client_id=acme"</c> in webhooks or embedded widgets),
    /// falling back to the client IP address if absent.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="queryParameterName">The query parameter name to inspect.</param>
    /// <param name="prefix">Optional prefix for the partition key. If null, the parameter name in lowercase is used.</param>
    /// <returns>The query parameter partition key, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetQueryKey(HttpContext context, string queryParameterName, string? prefix = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryParameterName);

        if (context.Request.Query.TryGetValue(queryParameterName, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var p = prefix ?? queryParameterName.Trim().ToLowerInvariant();
                return $"{p}_{raw}";
            }
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on the HTTP request <c>Host</c> header or domain name,
    /// enabling domain-isolated quotas for multi-tenant vanity domains, white-label portals, and multi-app gateways.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="includePort">Whether to include the port number if present. Defaults to <c>false</c> (domain only).</param>
    /// <returns>The host partition key formatted as <c>"host_{domain}"</c>.</returns>
    public static string GetHostKey(HttpContext context, bool includePort = false)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = includePort ? context.Request.Host.Value : context.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host))
            return "host_unknown";

        return $"host_{host.Trim().ToLowerInvariant()}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the HTTP request host/domain with a client partition key.
    /// Prevents a client from exceeding their per-domain quota across distinct white-label domains or vanity URLs.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="includePort">Whether to include the port number if present. Defaults to <c>false</c>.</param>
    /// <returns>A compound key formatted as <c>"{hostKey}:{clientKey}"</c>.</returns>
    public static string GetHostAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null, bool includePort = false)
    {
        ArgumentNullException.ThrowIfNull(context);

        var hostKey = GetHostKey(context, includePort);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{hostKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the YARP destination upstream cluster identifier with a client partition key.
    /// Prevents client quota exhaustion on fragile or compute-heavy backend clusters (e.g. analytics or reporting) from starving other services.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"cluster_{clusterId}:{clientKey}"</c>.</returns>
    public static string GetClusterAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var clusterId = ExtractClusterId(context.GetEndpoint())
                     ?? "unknown_cluster";

        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"cluster_{clusterId}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the HTTP <c>Origin</c> header,
    /// enabling rate limiting and fair-share isolation for third-party Single Page Applications (SPAs) and partner web integrations.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The origin partition key formatted as <c>"origin_{host}"</c>, or <c>"origin_unknown"</c>.</returns>
    public static string GetOriginKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Origin", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim();
            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                return $"origin_{uri.Host.ToLowerInvariant()}";

            return $"origin_{raw.ToLowerInvariant()}";
        }

        return "origin_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the HTTP <c>Origin</c> domain with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{originKey}:{clientKey}"</c>.</returns>
    public static string GetOriginAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var originKey = GetOriginKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{originKey}:{clientKey}";
    }

    private static readonly string[] DefaultVersionHeaders =
    [
        "X-Version",
        "X-API-Version",
        "api-version"
    ];

    private static readonly string[] DefaultVersionQueryKeys =
    [
        "api-version",
        "v",
        "version"
    ];

    /// <summary>
    /// Resolves a partition key based on the targeted API version extracted from headers, query string, or URL path segments.
    /// Allows throttling legacy, deprecated API versions to incentivize client upgrades while maintaining high capacity for modern versions.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="versionHeaderCandidates">Optional prioritized list of header names to search for API version.</param>
    /// <param name="versionQueryCandidates">Optional prioritized list of query parameter names to search for API version.</param>
    /// <returns>The API version partition key formatted as <c>"version_{version}"</c>, or <c>"version_unknown"</c>.</returns>
    public static string GetApiVersionKey(
        HttpContext context,
        string[]? versionHeaderCandidates = null,
        string[]? versionQueryCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = versionHeaderCandidates is { Length: > 0 } ? versionHeaderCandidates : DefaultVersionHeaders;
        foreach (var header in headers)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"version_{raw.ToLowerInvariant()}";
            }
        }

        var queries = versionQueryCandidates is { Length: > 0 } ? versionQueryCandidates : DefaultVersionQueryKeys;
        foreach (var q in queries)
        {
            if (context.Request.Query.TryGetValue(q, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"version_{raw.ToLowerInvariant()}";
            }
        }

        var path = context.Request.Path.Value;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                if (seg.Length >= 2 && (seg[0] == 'v' || seg[0] == 'V') && char.IsDigit(seg[1]))
                    return $"version_{seg.ToLowerInvariant()}";
            }
        }

        return "version_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the targeted API version with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="versionHeaderCandidates">Optional prioritized list of header names to search for API version.</param>
    /// <param name="versionQueryCandidates">Optional prioritized list of query parameter names to search for API version.</param>
    /// <returns>A compound key formatted as <c>"{versionKey}:{clientKey}"</c>.</returns>
    public static string GetApiVersionAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? versionHeaderCandidates = null,
        string[]? versionQueryCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var verKey = GetApiVersionKey(context, versionHeaderCandidates, versionQueryCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{verKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the HTTP <c>Referer</c> source domain.
    /// Throttles referral spam campaigns and isolates traffic originating from specific third-party websites or marketing links.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The referer domain partition key formatted as <c>"ref_{domain}"</c>, or <c>"ref_direct"</c>.</returns>
    public static string GetRefererKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Referer", out var refVal) && !string.IsNullOrWhiteSpace(refVal))
        {
            var raw = refVal.ToString().Trim();
            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                return $"ref_{uri.Host.ToLowerInvariant()}";

            return $"ref_{raw.ToLowerInvariant()}";
        }

        return "ref_direct";
    }

    /// <summary>
    /// Resolves a compound partition key combining the HTTP <c>Referer</c> domain with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{refererKey}:{clientKey}"</c>.</returns>
    public static string GetRefererAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var refKey = GetRefererKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{refKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the client primary language extracted from the <c>Accept-Language</c> header.
    /// Isolates localized traffic spikes during region-specific seasonal marketing campaigns or localized event releases.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The locale language partition key formatted as <c>"lang_{code}"</c>, or <c>"lang_default"</c>.</returns>
    public static string GetLocaleKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Accept-Language", out var langVal) && !string.IsNullOrWhiteSpace(langVal))
        {
            var raw = langVal.ToString();
            var firstLang = raw.Split(',', ';')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstLang))
            {
                var primary = firstLang.Split('-')[0].Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(primary))
                    return $"lang_{primary}";
            }
        }

        return "lang_default";
    }

    /// <summary>
    /// Resolves a compound partition key combining the client language locale with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{localeKey}:{clientKey}"</c>.</returns>
    public static string GetLocaleAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var langKey = GetLocaleKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{langKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on query parameter cardinality (<c>"query_complex"</c> vs <c>"query_simple"</c>).
    /// Defends against Deep Filtering DoS where attackers send excessive query parameters to force CPU and memory exhaustion in LINQ/Model-Binding pipelines.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="maxSimpleQueryCount">The maximum query parameter count classified as simple. Defaults to 10.</param>
    /// <returns><c>"query_complex"</c> if count exceeds threshold; otherwise <c>"query_simple"</c>.</returns>
    public static string GetQueryComplexityBucketKey(HttpContext context, int maxSimpleQueryCount = 10)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSimpleQueryCount);

        return context.Request.Query.Count > maxSimpleQueryCount ? "query_complex" : "query_simple";
    }

    /// <summary>
    /// Resolves a compound partition key combining query complexity with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="maxSimpleQueryCount">Maximum query count classified as simple.</param>
    /// <returns>A compound key formatted as <c>"{queryBucket}:{clientKey}"</c>.</returns>
    public static string GetQueryComplexityBucketAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int maxSimpleQueryCount = 10)
    {
        ArgumentNullException.ThrowIfNull(context);

        var queryKey = GetQueryComplexityBucketKey(context, maxSimpleQueryCount);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{queryKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on HTTP method safety semantics (RFC 9110).
    /// Categorizes requests into <c>"method_safe"</c> (GET, HEAD, OPTIONS, TRACE), <c>"method_idempotent"</c> (PUT, DELETE), and <c>"method_mutation"</c> (POST, PATCH),
    /// shielding upstream relational databases from write locks and mutation flood attacks.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The method safety bucket partition key.</returns>
    public static string GetMethodSafetyBucketKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
            return "method_safe";

        if (HttpMethods.IsPut(method) || HttpMethods.IsDelete(method))
            return "method_idempotent";

        if (HttpMethods.IsPost(method) || HttpMethods.IsPatch(method))
            return "method_mutation";

        return "method_other";
    }

    /// <summary>
    /// Resolves a compound partition key combining HTTP method safety with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{methodSafetyKey}:{clientKey}"</c>.</returns>
    public static string GetMethodSafetyBucketAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var safetyKey = GetMethodSafetyBucketKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{safetyKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the route endpoint SLA criticality tier (<c>"sla_critical"</c>, <c>"sla_standard"</c>, <c>"sla_background"</c>) extracted from ASP.NET Core Route Metadata.
    /// Ensures mission-critical payment and authentication routes receive dedicated rate limit budgets without cross-talk from background analytics routes.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="metadataTierKey">The key to inspect in the route metadata dictionary. Defaults to <c>"tier"</c>.</param>
    /// <returns>The SLA tier partition key formatted as <c>"sla_{tier}"</c>.</returns>
    public static string GetEndpointTierKey(HttpContext context, string metadataTierKey = "tier")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataTierKey);

        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            var meta = endpoint.Metadata.GetMetadata<IKyrolusRateLimitMetadata>();
            if (!string.IsNullOrWhiteSpace(meta?.SlaTier))
                return $"sla_{meta.SlaTier.Trim().ToLowerInvariant()}";

            var dictVal = ExtractMetadataValue(endpoint, metadataTierKey);
            if (!string.IsNullOrWhiteSpace(dictVal))
                return $"sla_{dictVal.Trim().ToLowerInvariant()}";
        }

        return "sla_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the route endpoint SLA criticality tier with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="metadataTierKey">The key to inspect in the route metadata dictionary.</param>
    /// <returns>A compound key formatted as <c>"{tierKey}:{clientKey}"</c>.</returns>
    public static string GetEndpointTierAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string metadataTierKey = "tier")
    {
        ArgumentNullException.ThrowIfNull(context);

        var tierKey = GetEndpointTierKey(context, metadataTierKey);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{tierKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the endpoint API deprecation and lifecycle status (<c>"api_sunset"</c>, <c>"api_deprecated"</c>, <c>"api_active"</c>, RFC 8594).
    /// Enforces strict rate limits on legacy sunsetting endpoints to accelerate client migration to current API versions.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The API deprecation partition key.</returns>
    public static string GetApiDeprecationStateKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Check Request Headers
        if (context.Request.Headers.TryGetValue("X-Api-Sunset", out var sunsetHeader) &&
            (string.Equals(sunsetHeader, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(sunsetHeader, "1", StringComparison.OrdinalIgnoreCase)))
        {
            return "api_sunset";
        }

        if (context.Request.Headers.TryGetValue("X-Api-Deprecated", out var depHeader) &&
            (string.Equals(depHeader, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(depHeader, "1", StringComparison.OrdinalIgnoreCase)))
        {
            return "api_deprecated";
        }

        // 2. Check Endpoint Route Metadata
        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            if (endpoint.Metadata.GetMetadata<ObsoleteAttribute>() is not null)
                return "api_deprecated";

            var sunset = ExtractMetadataValue(endpoint, "Sunset");
            if (string.Equals(sunset, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(sunset, "1", StringComparison.OrdinalIgnoreCase))
                return "api_sunset";

            var dep = ExtractMetadataValue(endpoint, "Deprecated");
            if (string.Equals(dep, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(dep, "1", StringComparison.OrdinalIgnoreCase))
                return "api_deprecated";
        }

        return "api_active";
    }

    /// <summary>
    /// Resolves a compound partition key combining the API deprecation lifecycle state with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{deprecationKey}:{clientKey}"</c>.</returns>
    public static string GetApiDeprecationStateAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deprecationKey = GetApiDeprecationStateKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{deprecationKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key distinguishing multi-tenant vanity subdomains from shared apex domains (<c>"tenant_vanity_subdomain"</c> vs <c>"tenant_shared_apex"</c>).
    /// Analyzes the HTTP Host header against an optional apex domain to partition tenant traffic on branded customer subdomains.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="baseApexDomain">Optional root apex domain (e.g. <c>"kyrolus.com"</c>) to differentiate vanity subdomains from top-level apex traffic.</param>
    /// <returns><c>"tenant_vanity_subdomain"</c> if the request is routed through a customer vanity subdomain; otherwise <c>"tenant_shared_apex"</c>.</returns>
    public static string GetTenantSubdomainTierKey(HttpContext context, string? baseApexDomain = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host) || IPAddress.TryParse(host, out _))
            return "tenant_shared_apex";

        var cleanHost = host.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(baseApexDomain))
        {
            var cleanApex = baseApexDomain.Trim().TrimStart('.').ToLowerInvariant();
            if (cleanHost.EndsWith($".{cleanApex}", StringComparison.Ordinal) &&
                cleanHost.Length > cleanApex.Length + 1)
            {
                var subdomain = cleanHost[..^(cleanApex.Length + 1)];
                if (!string.Equals(subdomain, "www", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(subdomain, "api", StringComparison.OrdinalIgnoreCase))
                {
                    return "tenant_vanity_subdomain";
                }
            }
            return "tenant_shared_apex";
        }

        var parts = cleanHost.Split('.');
        if (parts.Length > 2 && !string.Equals(parts[0], "www", StringComparison.OrdinalIgnoreCase) && !string.Equals(parts[0], "api", StringComparison.OrdinalIgnoreCase))
        {
            return "tenant_vanity_subdomain";
        }

        return "tenant_shared_apex";
    }

    /// <summary>
    /// Resolves a compound partition key combining the tenant vanity subdomain classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="baseApexDomain">Optional root apex domain (e.g. <c>"kyrolus.com"</c>) to differentiate vanity subdomains.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{subdomainKey}:{clientKey}"</c>.</returns>
    public static string GetTenantSubdomainTierAndClientKey(
        HttpContext context,
        string? baseApexDomain = null,
        Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var subdomainKey = GetTenantSubdomainTierKey(context, baseApexDomain);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{subdomainKey}:{clientKey}";
    }

    private static string? ExtractClusterId(Endpoint? endpoint)
    {
        if (endpoint is null) return null;
        foreach (var item in endpoint.Metadata)
        {
            var type = item.GetType();
            var clusterProp = type.GetProperty("ClusterId");
            if (clusterProp?.GetValue(item) is string cid && !string.IsNullOrWhiteSpace(cid))
                return cid;

            var configProp = type.GetProperty("Config");
            if (configProp?.GetValue(item) is object cfg)
            {
                var cfgCluster = cfg.GetType().GetProperty("ClusterId")?.GetValue(cfg) as string;
                if (!string.IsNullOrWhiteSpace(cfgCluster))
                    return cfgCluster;
            }
        }
        return null;
    }

    private static string? ExtractMetadataValue(Endpoint? endpoint, string key)
    {
        if (endpoint is null) return null;
        foreach (var item in endpoint.Metadata)
        {
            if (item is IReadOnlyDictionary<string, string> dict && dict.TryGetValue(key, out var val))
                return val;

            if (item is IKyrolusRateLimitMetadata rlm)
            {
                if (string.Equals(key, "tier", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rlm.SlaTier))
                    return rlm.SlaTier;
                if (string.Equals(key, "policy", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rlm.PolicyName))
                    return rlm.PolicyName;
            }

            var type = item.GetType();
            var metaProp = type.GetProperty("Metadata");
            if (metaProp?.GetValue(item) is IReadOnlyDictionary<string, string> mDict && mDict.TryGetValue(key, out var v1))
                return v1;

            var configProp = type.GetProperty("Config");
            if (configProp?.GetValue(item) is object cfg)
            {
                var cfgMeta = cfg.GetType().GetProperty("Metadata")?.GetValue(cfg) as IReadOnlyDictionary<string, string>;
                if (cfgMeta?.TryGetValue(key, out var v2) == true)
                    return v2;
            }
        }
        return null;
    }
}
