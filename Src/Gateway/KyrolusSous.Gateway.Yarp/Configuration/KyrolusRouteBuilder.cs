namespace KyrolusSous.Gateway.Yarp.Configuration;

/// <summary>
/// Fluent builder for configuring individual route matching, security policies, timeouts, and URL transforms.
/// </summary>
public sealed class KyrolusRouteBuilder
{
    private readonly string _routeId;
    private readonly string _clusterId;
    private readonly string _path;
    private readonly List<string> _methods = [];
    private readonly List<string> _hosts = [];
    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IReadOnlyDictionary<string, string>> _transforms = [];
    private string? _authorizationPolicy;
    private string? _corsPolicy;
    private string? _rateLimiterPolicy;
    private string? _outputCachePolicy;
    private TimeSpan? _timeout;
    private long? _maxRequestBodySize;
    private int? _order;
    private readonly List<KyrolusRouteHeader> _headers = [];
    private readonly List<KyrolusRouteQueryParameter> _queryParameters = [];
    private bool _requireTenant;
    private List<string>? _allowedContentTypes;
    private KyrolusIpFilterOptions? _ipFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusRouteBuilder"/> class.
    /// </summary>
    public KyrolusRouteBuilder(string routeId, string clusterId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _routeId = routeId;
        _clusterId = clusterId;
        _path = path;
    }

    /// <summary>
    /// Restricts this route to the specified HTTP methods.
    /// Methods are automatically normalized to uppercase to conform with RFC 9110.
    /// Use constants from <see cref="KyrolusGatewayHttpMethods"/>.
    /// </summary>
    public KyrolusRouteBuilder WithMethods(params string[] methods)
    {
        if (methods is { Length: > 0 })
        {
            _methods.AddRange(methods.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim().ToUpperInvariant()));
        }
        return this;
    }

    /// <summary>
    /// Restricts this route to the specified incoming client request hostnames / domains (e.g., <c>""api.example.com""</c>).
    /// </summary>
    public KyrolusRouteBuilder WithHosts(params string[] hosts)
    {
        if (hosts is { Length: > 0 })
        {
            _hosts.AddRange(hosts);
        }
        return this;
    }

    /// <summary>
    /// Restricts this route to requests containing the specified HTTP header matching any of the given values.
    /// Used for canary releases, API versioning, and client targeting.
    /// </summary>
    /// <param name="headerName">The header name (e.g. <c>"X-API-Version"</c>).</param>
    /// <param name="values">Acceptable header values (e.g. <c>"2"</c>, <c>"v2"</c>).</param>
    /// <param name="mode">The match mode (e.g. <c>"ExactHeader"</c>, <c>"HeaderPrefix"</c>, <c>"Exists"</c>, <c>"NotExists"</c>). Defaults to <c>"ExactHeader"</c>.</param>
    /// <param name="isCaseSensitive">Whether value matching is case-sensitive. Defaults to <c>false</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithHeaderMatch(
        string headerName,
        IReadOnlyList<string>? values = null,
        string mode = "ExactHeader",
        bool isCaseSensitive = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        _headers.Add(new KyrolusRouteHeader
        {
            Name = headerName,
            Values = values,
            Mode = mode,
            IsCaseSensitive = isCaseSensitive
        });
        return this;
    }

    /// <summary>
    /// Restricts this route to requests containing the specified HTTP header matching any of the given values.
    /// </summary>
    /// <param name="headerName">The header name (e.g. <c>"X-API-Version"</c>).</param>
    /// <param name="values">Acceptable header values (e.g. <c>"2"</c>, <c>"v2"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithHeaderMatch(string headerName, params string[] values)
        => WithHeaderMatch(headerName, values is { Length: > 0 } ? values.ToList().AsReadOnly() : null, "ExactHeader", false);

    /// <summary>
    /// Restricts this route to requests where the specified HTTP header exists (with any non-empty value).
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithHeaderExists(string headerName)
        => WithHeaderMatch(headerName, values: null, mode: "Exists", isCaseSensitive: false);

    /// <summary>
    /// Restricts this route to requests containing the specified query parameter matching any of the given values.
    /// </summary>
    /// <param name="parameterName">The query string parameter key.</param>
    /// <param name="values">Acceptable query parameter values.</param>
    /// <param name="mode">The match mode (e.g. <c>"Exact"</c>, <c>"Prefix"</c>, <c>"Contains"</c>, <c>"NotContains"</c>, <c>"Exists"</c>). Defaults to <c>"Exact"</c>.</param>
    /// <param name="isCaseSensitive">Whether value matching is case-sensitive. Defaults to <c>false</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithQueryMatch(
        string parameterName,
        IReadOnlyList<string>? values = null,
        string mode = "Exact",
        bool isCaseSensitive = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        _queryParameters.Add(new KyrolusRouteQueryParameter
        {
            Name = parameterName,
            Values = values,
            Mode = mode,
            IsCaseSensitive = isCaseSensitive
        });
        return this;
    }

    /// <summary>
    /// Restricts this route to requests containing the specified query parameter matching any of the given values.
    /// </summary>
    /// <param name="parameterName">The query string parameter key.</param>
    /// <param name="values">Acceptable query parameter values.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithQueryMatch(string parameterName, params string[] values)
        => WithQueryMatch(parameterName, values is { Length: > 0 } ? values.ToList().AsReadOnly() : null, "Exact", false);

    /// <summary>
    /// Restricts this route to requests where the specified query string parameter exists.
    /// </summary>
    /// <param name="parameterName">The query string parameter key.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithQueryExists(string parameterName)
        => WithQueryMatch(parameterName, values: null, mode: "Exists", isCaseSensitive: false);

    /// <summary>
    /// Strictly requires a valid authenticated multi-tenant context to access this route.
    /// Requests without a verified tenant are rejected at the edge with HTTP 401 Unauthorized.
    /// </summary>
    /// <param name="require">Whether a tenant is required. Defaults to <c>true</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithRequireTenant(bool require = true)
    {
        _requireTenant = require;
        _metadata["Kyrolus:Tenant:Required"] = require ? "true" : "false";
        return this;
    }

    /// <summary>
    /// Restricts incoming request bodies to the specified Content-Type MIME types (e.g. <c>"application/json"</c>).
    /// Defends against XXE and unexpected deserialization payload attacks by rejecting unsupported types with HTTP 415.
    /// </summary>
    /// <param name="allowedContentTypes">Allowed MIME types (e.g., <c>"application/json"</c>, <c>"text/plain"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithAllowedContentTypes(params string[] allowedContentTypes)
    {
        if (allowedContentTypes is { Length: > 0 })
        {
            _allowedContentTypes = [.. allowedContentTypes];
            _metadata["Kyrolus:ContentType:Allowed"] = string.Join(",", allowedContentTypes);
        }
        return this;
    }

    /// <summary>
    /// Configures X-Forwarded-* proxy header transform actions to sanitize client-supplied headers and defend against IP spoofing.
    /// </summary>
    /// <param name="forAction">Action for X-Forwarded-For (<c>"Set"</c>, <c>"Append"</c>, <c>"Off"</c>). Defaults to <c>"Set"</c>.</param>
    /// <param name="protoAction">Action for X-Forwarded-Proto (<c>"Set"</c>, <c>"Append"</c>, <c>"Off"</c>). Defaults to <c>"Set"</c>.</param>
    /// <param name="hostAction">Action for X-Forwarded-Host (<c>"Set"</c>, <c>"Append"</c>, <c>"Off"</c>). Defaults to <c>"Set"</c>.</param>
    /// <param name="prefixAction">Action for X-Forwarded-Prefix (<c>"Set"</c>, <c>"Append"</c>, <c>"Off"</c>). Defaults to <c>"Set"</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformForwarded(
        string forAction = "Set",
        string protoAction = "Set",
        string hostAction = "Set",
        string prefixAction = "Set")
    {
        _transforms.Add(new Dictionary<string, string>
        {
            ["X-Forwarded"] = $"{forAction},{protoAction},{hostAction},{prefixAction}"
        });
        return this;
    }

    /// <summary>
    /// Enforces an ASP.NET Core authorization policy on this route at the gateway edge.
    /// </summary>
    public KyrolusRouteBuilder WithAuthorization(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _authorizationPolicy = policy;
        return this;
    }

    /// <summary>
    /// Enforces an ASP.NET Core CORS policy on this route.
    /// </summary>
    public KyrolusRouteBuilder WithCors(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _corsPolicy = policy;
        return this;
    }

    /// <summary>
    /// Enforces an ASP.NET Core rate limiter policy on this route at the gateway perimeter.
    /// </summary>
    public KyrolusRouteBuilder WithRateLimiter(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _rateLimiterPolicy = policy;
        return this;
    }

    /// <summary>
    /// Sets a processing timeout for requests matching this route.
    /// </summary>
    public KyrolusRouteBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Adds a path transform that strips the specified prefix from the request URL before forwarding to the backend.
    /// E.g. <c>""/api/orders/123""</c> with prefix <c>""/api""</c> becomes <c>""/orders/123""</c>.
    /// </summary>
    public KyrolusRouteBuilder WithTransformPathRemovePrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        _transforms.Add(new Dictionary<string, string> { ["PathRemovePrefix"] = prefix });
        return this;
    }

    /// <summary>
    /// Adds a path transform that prepends the specified prefix to the request URL before forwarding to the backend.
    /// </summary>
    public KyrolusRouteBuilder WithTransformPathPrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        _transforms.Add(new Dictionary<string, string> { ["PathPrefix"] = prefix });
        return this;
    }

    /// <summary>
    /// Enforces an ASP.NET Core output caching policy on this route at the gateway edge.
    /// Responses matching this route will be cached at the reverse proxy perimeter according to the policy rules.
    /// </summary>
    /// <param name="policy">The named output cache policy.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithOutputCache(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _outputCachePolicy = policy;
        return this;
    }

    /// <summary>
    /// Suppresses the injection of the <c>X-Kyrolus-Gateway: Active</c> telemetry response header on this route.
    /// Defends against Information Disclosure (CWE-200) by hiding gateway implementation details from external clients.
    /// </summary>
    /// <param name="suppress">Whether to suppress the telemetry header. Defaults to <c>true</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithSuppressTelemetryHeader(bool suppress = true)
    {
        _metadata["Kyrolus:SuppressTelemetryHeader"] = suppress ? "true" : "false";
        return this;
    }

    /// <summary>
    /// Configures whether HTTP method override headers (<c>X-HTTP-Method-Override</c>, <c>X-HTTP-Method</c>) are permitted on this route.
    /// Defends against HTTP Verb Tampering (CWE-287 / CWE-654) by strictly validating the overridden method against declared route methods.
    /// </summary>
    /// <param name="allow">Whether method override is allowed. Defaults to <c>true</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithAllowMethodOverride(bool allow = true)
    {
        _metadata["Kyrolus:MethodOverride:Allowed"] = allow ? "true" : "false";
        return this;
    }

    /// <summary>
    /// Configures whether verified mTLS client certificate details (Thumbprint, Subject, Issuer) should be forwarded to the backend.
    /// Defends against Client Certificate Spoofing (CWE-295) by stripping untrusted client-supplied headers.
    /// </summary>
    /// <param name="enable">Whether to forward client certificate details. Defaults to <c>true</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithClientCertForwarding(bool enable = true)
    {
        _metadata["Kyrolus:ClientCert:Forward"] = enable ? "true" : "false";
        return this;
    }

    /// <summary>
    /// Injects a custom <c>Content-Security-Policy</c> (CSP) header on responses matching this route.
    /// Defends against Cross-Site Scripting (XSS) and data injection attacks (CWE-79).
    /// </summary>
    /// <param name="csp">The Content-Security-Policy directive string.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithContentSecurityPolicy(string csp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csp);
        _metadata["Kyrolus:SecurityHeaders:CSP"] = csp;
        return this;
    }

    /// <summary>
    /// Overrides the default <c>X-Frame-Options: DENY</c> header on responses matching this route (e.g. <c>"SAMEORIGIN"</c> for embedded widgets).
    /// </summary>
    /// <param name="frameOptions">The frame options directive (e.g. <c>"SAMEORIGIN"</c> or <c>"DENY"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithFrameOptions(string frameOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameOptions);
        _metadata["Kyrolus:SecurityHeaders:FrameOptions"] = frameOptions;
        return this;
    }

    /// <summary>
    /// Overrides the default <c>Referrer-Policy</c> header on responses matching this route.
    /// </summary>
    /// <param name="policy">The referrer policy directive (e.g. <c>"no-referrer"</c>, <c>"strict-origin-when-cross-origin"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithReferrerPolicy(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _metadata["Kyrolus:SecurityHeaders:ReferrerPolicy"] = policy;
        return this;
    }

    /// <summary>
    /// Adds a request transform that rewrites the <c>Host</c> header sent to the backend destination to match internal hostnames or SNI requirements.
    /// </summary>
    /// <param name="host">The target internal backend host name (e.g. <c>"order-service.internal:5000"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        _transforms.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RequestHeader"] = "Host",
            ["Set"] = host
        });
        return this;
    }

    /// <summary>
    /// Adds a request header transform that sets or appends a header on proxied requests sent to the backend.
    /// </summary>
    /// <param name="headerName">The header name to set or append.</param>
    /// <param name="value">The value of the header.</param>
    /// <param name="append"><c>true</c> to append to existing values; <c>false</c> to overwrite/set.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformRequestHeader(string headerName, string value, bool append = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        _transforms.Add(new Dictionary<string, string>
        {
            ["RequestHeader"] = headerName,
            [append ? "Append" : "Set"] = value ?? string.Empty
        });
        return this;
    }

    /// <summary>
    /// Adds a request header transform that removes the specified header from proxied requests before forwarding to the backend.
    /// </summary>
    /// <param name="headerName">The header name to strip.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformRequestHeaderRemove(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        _transforms.Add(new Dictionary<string, string> { ["RequestHeaderRemove"] = headerName });
        return this;
    }

    /// <summary>
    /// Adds a response header transform that sets or appends a header on responses returned to the client.
    /// </summary>
    /// <param name="headerName">The response header name.</param>
    /// <param name="value">The value to set or append.</param>
    /// <param name="append"><c>true</c> to append to existing values; <c>false</c> to overwrite/set.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformResponseHeader(string headerName, string value, bool append = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        _transforms.Add(new Dictionary<string, string>
        {
            ["ResponseHeader"] = headerName,
            [append ? "Append" : "Set"] = value ?? string.Empty
        });
        return this;
    }

    /// <summary>
    /// Adds a response header transform that removes the specified header from responses before delivery to the client.
    /// </summary>
    /// <param name="headerName">The response header name to strip.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformResponseHeaderRemove(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        _transforms.Add(new Dictionary<string, string> { ["ResponseHeaderRemove"] = headerName });
        return this;
    }

    /// <summary>
    /// Adds transforms that strip the specified header from both inbound proxied requests and outbound client responses.
    /// Defends against information leakage and header tampering.
    /// </summary>
    /// <param name="headerName">The HTTP header name to strip.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithStripHeader(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        WithTransformRequestHeaderRemove(headerName);
        WithTransformResponseHeaderRemove(headerName);
        return this;
    }

    /// <summary>
    /// Adds a query parameter transform that sets or appends a static query parameter on proxied requests.
    /// </summary>
    /// <param name="queryKey">The query string parameter key.</param>
    /// <param name="value">The query string value.</param>
    /// <param name="append"><c>true</c> to append; <c>false</c> to overwrite.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformQueryValueParameter(string queryKey, string value, bool append = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryKey);
        _transforms.Add(new Dictionary<string, string>
        {
            ["QueryValueParameter"] = queryKey,
            [append ? "Append" : "Set"] = value ?? string.Empty
        });
        return this;
    }

    /// <summary>
    /// Adds a query parameter transform that sets or appends a query parameter using a value matched from the route template.
    /// </summary>
    /// <param name="queryKey">The query string parameter key.</param>
    /// <param name="routeKey">The route template parameter name.</param>
    /// <param name="append"><c>true</c> to append; <c>false</c> to overwrite.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformQueryRouteParameter(string queryKey, string routeKey, bool append = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        _transforms.Add(new Dictionary<string, string>
        {
            ["QueryRouteParameter"] = queryKey,
            [append ? "Append" : "Set"] = routeKey
        });
        return this;
    }

    /// <summary>
    /// Adds a path transform that replaces the request URL with the specified fixed path.
    /// </summary>
    public KyrolusRouteBuilder WithTransformPathSet(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _transforms.Add(new Dictionary<string, string> { ["PathSet"] = path });
        return this;
    }

    /// <summary>
    /// Adds a forwarded transform configuring proxy forwarding headers (X-Forwarded-* or RFC 7239 Forwarded).
    /// </summary>
    /// <param name="useXForwarded">Whether to use legacy <c>X-Forwarded-*</c> headers instead of RFC 7239 <c>Forwarded</c>. Defaults to <c>true</c>.</param>
    /// <param name="forFormat">Format for the For header (e.g. <c>"Random"</c>, <c>"Ip"</c>, <c>"IpAndPort"</c>). If null, preserves existing.</param>
    /// <param name="byFormat">Format for the By header (e.g. <c>"Random"</c>, <c>"Ip"</c>). If null, preserves existing.</param>
    /// <param name="host">Whether to enable Host forwarding transform action.</param>
    /// <param name="proto">Whether to enable Proto / Scheme forwarding transform action.</param>
    /// <param name="prefix">Custom header prefix (e.g. <c>"X-Forwarded"</c> or <c>"Forwarded"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithTransformForwarded(
        bool useXForwarded = true,
        string? forFormat = null,
        string? byFormat = null,
        bool host = true,
        bool proto = true,
        string? prefix = null)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var key = prefix ?? (useXForwarded ? "X-Forwarded" : "Forwarded");
        var parts = new List<string>();
        if (proto) parts.Add("proto");
        if (host) parts.Add("host");
        if (forFormat != null) parts.Add("for");
        if (byFormat != null) parts.Add("by");

        dict[key] = string.Join(",", parts);
        if (forFormat != null)
        {
            dict["ForFormat"] = forFormat;
        }
        if (byFormat != null)
        {
            dict["ByFormat"] = byFormat;
        }
        if (prefix != null)
        {
            dict["Prefix"] = prefix;
        }

        _transforms.Add(dict);
        return this;
    }

    /// <summary>
    /// Sets the maximum allowed request body size in bytes for this route.
    /// Defends against denial-of-service (DoS) and memory exhaustion attacks via oversized payloads.
    /// </summary>
    /// <param name="bytes">The maximum payload size in bytes.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithMaxRequestBodySize(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        _maxRequestBodySize = bytes;
        return this;
    }

    /// <summary>
    /// Attaches custom metadata to the route.
    /// </summary>
    public KyrolusRouteBuilder WithMetadata(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata[key] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Sets the evaluation order priority for this route. Lower numerical values have higher matching precedence.
    /// Resolves ambiguity when multiple overlapping route templates match the incoming request.
    /// </summary>
    /// <param name="order">The evaluation order number (e.g., 1 for high priority, 100 for fallback catch-all).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithOrder(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>
    /// Restricts access to this route to the specified client IP addresses or CIDR blocks.
    /// Callers connecting from any other IP will be rejected with HTTP 403 Forbidden.
    /// </summary>
    /// <param name="allowedIpsOrCidrs">Allowed IPv4/IPv6 addresses or CIDR blocks (e.g. <c>"10.0.0.0/8"</c>, <c>"192.168.1.100"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithIpAllowlist(params string[] allowedIpsOrCidrs)
    {
        if (allowedIpsOrCidrs is { Length: > 0 })
        {
            var existingBlocked = _ipFilter?.BlockedIpsOrCidrs;
            _ipFilter = new KyrolusIpFilterOptions
            {
                AllowedIpsOrCidrs = allowedIpsOrCidrs.ToList().AsReadOnly(),
                BlockedIpsOrCidrs = existingBlocked
            };
            _metadata["Kyrolus:IpFilter:Allowed"] = string.Join(",", allowedIpsOrCidrs);
        }
        return this;
    }

    /// <summary>
    /// Denies access to this route for the specified client IP addresses or CIDR blocks.
    /// Callers connecting from any blocked IP will be rejected with HTTP 403 Forbidden.
    /// </summary>
    /// <param name="blockedIpsOrCidrs">Blocked IPv4/IPv6 addresses or CIDR blocks (e.g. <c>"203.0.113.50"</c>, <c>"198.51.100.0/24"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithIpBlocklist(params string[] blockedIpsOrCidrs)
    {
        if (blockedIpsOrCidrs is { Length: > 0 })
        {
            var existingAllowed = _ipFilter?.AllowedIpsOrCidrs;
            _ipFilter = new KyrolusIpFilterOptions
            {
                AllowedIpsOrCidrs = existingAllowed,
                BlockedIpsOrCidrs = blockedIpsOrCidrs.ToList().AsReadOnly()
            };
            _metadata["Kyrolus:IpFilter:Blocked"] = string.Join(",", blockedIpsOrCidrs);
        }
        return this;
    }

    /// <summary>
    /// Enforces maximum request header count and total header size limits on this route to defend against Slowloris and header DoS attacks (CWE-400).
    /// Requests exceeding either threshold will be rejected with HTTP 431 Request Header Fields Too Large.
    /// </summary>
    /// <param name="maxCount">The maximum number of HTTP headers permitted (e.g. <c>100</c>).</param>
    /// <param name="maxTotalLengthBytes">The maximum combined byte length of header names and values (e.g. <c>32768</c> for 32 KB).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithMaxHeaderLimits(int maxCount, int maxTotalLengthBytes)
    {
        if (maxCount > 0)
        {
            _metadata["Kyrolus:Headers:MaxCount"] = maxCount.ToString();
        }
        if (maxTotalLengthBytes > 0)
        {
            _metadata["Kyrolus:Headers:MaxTotalLength"] = maxTotalLengthBytes.ToString();
        }
        return this;
    }

    /// <summary>
    /// Configures custom HTTP Strict Transport Security (HSTS) header value for HTTPS responses on this route.
    /// </summary>
    /// <param name="hstsValue">The raw HSTS header value (e.g., <c>"max-age=63072000; includeSubDomains; preload"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithHsts(string hstsValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hstsValue);
        _metadata["Kyrolus:SecurityHeaders:HSTS"] = hstsValue;
        return this;
    }

    /// <summary>
    /// Configures custom HTTP Strict Transport Security (HSTS) parameters for HTTPS responses on this route.
    /// </summary>
    /// <param name="maxAge">The duration that browsers should remember that this host is only to be accessed using HTTPS.</param>
    /// <param name="includeSubDomains">Whether the rule applies to all of the site's subdomains as well.</param>
    /// <param name="preload">Whether to allow inclusion in the browser HSTS preload list.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusRouteBuilder WithHsts(TimeSpan maxAge, bool includeSubDomains = true, bool preload = false)
    {
        var seconds = (long)maxAge.TotalSeconds;
        var value = $"max-age={seconds}";
        if (includeSubDomains)
        {
            value += "; includeSubDomains";
        }
        if (preload)
        {
            value += "; preload";
        }
        return WithHsts(value);
    }

    /// <summary>
    /// Builds and returns the configured <see cref="KyrolusGatewayRoute"/> instance.
    /// </summary>
    public KyrolusGatewayRoute Build()
    {
        return new KyrolusGatewayRoute
        {
            RouteId = _routeId,
            ClusterId = _clusterId,
            Match = new KyrolusGatewayRouteMatch
            {
                Path = _path,
                Methods = _methods.Count > 0 ? _methods.AsReadOnly() : null,
                Hosts = _hosts.Count > 0 ? _hosts.AsReadOnly() : null,
                Headers = _headers.Count > 0 ? _headers.AsReadOnly() : null,
                QueryParameters = _queryParameters.Count > 0 ? _queryParameters.AsReadOnly() : null
            },
            AuthorizationPolicy = _authorizationPolicy,
            CorsPolicy = _corsPolicy,
            RateLimiterPolicy = _rateLimiterPolicy,
            OutputCachePolicy = _outputCachePolicy,
            Timeout = _timeout,
            MaxRequestBodySize = _maxRequestBodySize,
            Order = _order,
            IpFilter = _ipFilter,
            RequireTenant = _requireTenant,
            AllowedContentTypes = _allowedContentTypes is { Count: > 0 } ? _allowedContentTypes.AsReadOnly() : null,
            Transforms = _transforms.Count > 0 ? _transforms.AsReadOnly() : null,
            Metadata = _metadata.Count > 0 ? _metadata : null
        };
    }
}
