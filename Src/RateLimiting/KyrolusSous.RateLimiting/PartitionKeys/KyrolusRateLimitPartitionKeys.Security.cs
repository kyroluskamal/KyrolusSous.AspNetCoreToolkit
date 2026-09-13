namespace KyrolusSous.RateLimiting;

/// <summary>
/// Geo-location, ASN, TLS fingerprints, bot reputation scores, and threat detection partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a partition key based on an idempotency header (e.g. <c>"Idempotency-Key"</c> or <c>"X-Idempotency-Key"</c>),
    /// defending FinTech and financial transaction endpoints against rapid replay attacks and accidental client retry storms.
    /// Falls back to the client IP address if the header is absent.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="headerName">The idempotency header name to inspect. Defaults to <c>"Idempotency-Key"</c>.</param>
    /// <param name="prefix">Optional prefix for the partition key. Defaults to <c>"idempotency"</c>.</param>
    /// <returns>The idempotency partition key formatted as <c>"{prefix}_{key}"</c>, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetIdempotencyKey(HttpContext context, string headerName = "Idempotency-Key", string? prefix = "idempotency")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        if (context.Request.Headers.TryGetValue(headerName, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var p = prefix ?? "idempotency";
                return $"{p}_{raw}";
            }
        }

        // Secondary fallback to X-Idempotency-Key if the default header was specified and not found
        if (string.Equals(headerName, "Idempotency-Key", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Headers.TryGetValue("X-Idempotency-Key", out var altVal) && !string.IsNullOrWhiteSpace(altVal))
        {
            var raw = altVal.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var p = prefix ?? "idempotency";
                return $"{p}_{raw}";
            }
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    private static readonly string[] DefaultCountryHeaders =
    [
        "CF-IPCountry",               // Cloudflare
        "CloudFront-Viewer-Country",  // AWS CloudFront
        "X-Azure-ClientIPCountry",    // Azure Front Door
        "Fastly-Client-IP-Country",   // Fastly
        "X-Country-Code"              // Generic reverse proxies / HAProxy
    ];

    /// <summary>
    /// Resolves a partition key based on the client's geographic country code extracted from trusted CDN/proxy headers
    /// (such as <c>"CF-IPCountry"</c>, <c>"CloudFront-Viewer-Country"</c>, or <c>"X-Azure-ClientIPCountry"</c>).
    /// Enables pooled traffic damping during distributed volumetric DDoS attacks originating from specific geopolitical regions.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="countryHeaderCandidates">Optional prioritized list of header names to search for country code. If null, defaults to standard CDN headers.</param>
    /// <param name="fallbackCountry">The fallback country code to use when no header matches. Defaults to <c>"unknown"</c>.</param>
    /// <returns>The country partition key formatted as <c>"country_{code}"</c> in uppercase.</returns>
    public static string GetGeoCountryKey(HttpContext context, string[]? countryHeaderCandidates = null, string fallbackCountry = "unknown")
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = countryHeaderCandidates is { Length: > 0 } ? countryHeaderCandidates : DefaultCountryHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var code = val.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(code))
                    return $"country_{code.ToUpperInvariant()}";
            }
        }

        return $"country_{fallbackCountry.Trim().ToLowerInvariant()}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the geographic country code with a client partition key.
    /// Enables regional rate limiting while preserving individual client fairness within that country.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="countryHeaderCandidates">Optional prioritized list of header names to search for country code.</param>
    /// <returns>A compound key formatted as <c>"{countryKey}:{clientKey}"</c>.</returns>
    public static string GetGeoCountryAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null, string[]? countryHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var countryKey = GetGeoCountryKey(context, countryHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{countryKey}:{clientKey}";
    }

    private static readonly string[] DefaultFingerprintHeaders =
    [
        "User-Agent",
        "Accept-Language",
        "Sec-CH-UA",
        "Sec-CH-UA-Platform"
    ];

    /// <summary>
    /// Resolves a partition key based on a cryptographic SHA-256 synthetic fingerprint of client request characteristics
    /// (combining <c>User-Agent</c>, <c>Accept-Language</c>, <c>Sec-CH-UA</c>, and <c>Sec-CH-UA-Platform</c> headers).
    /// Tracks and throttles automated scrapers, crawlers, and botnets even when they rotate IP addresses across residential proxies or Tor networks.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="headersToInclude">Optional list of header names to include in the fingerprint calculation. Defaults to browser device hints.</param>
    /// <returns>The fingerprint partition key formatted as <c>"fingerprint_{hexHash:16}"</c>, or <c>"ip_{clientIp}"</c> fallback if headers are completely absent.</returns>
    public static string GetClientFingerprintKey(HttpContext context, string[]? headersToInclude = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = headersToInclude is { Length: > 0 } ? headersToInclude : DefaultFingerprintHeaders;
        var sb = new StringBuilder(128);

        foreach (var header in headers)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                sb.Append(header).Append(':').Append(val.ToString().Trim()).Append(';');
            }
        }

        if (sb.Length == 0)
            return $"ip_{GetClientIpKey(context)}";

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hashBytes = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();

        return $"fingerprint_{hex}";
    }

    private static readonly string[] DefaultAsnHeaders =
    [
        "CF-Connecting-ASN",         // Cloudflare
        "Fastly-Client-AS-Number",   // Fastly
        "X-Client-ASN",              // Generic reverse proxies / WAFs
        "X-ASN"                      // AWS CloudFront custom headers / HAProxy
    ];

    /// <summary>
    /// Resolves a partition key based on the client's Autonomous System Number (ASN),
    /// enabling pooled rate limit budgets across entire hosting providers and cloud data centers (e.g. AWS, DigitalOcean, Hetzner)
    /// to mitigate distributed botnets and automated scrapers without impacting residential ISP subscribers.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="asnHeaderCandidates">Optional prioritized list of header names to search for ASN. If null, defaults to standard CDN headers.</param>
    /// <returns>The ASN partition key formatted as <c>"asn_{number}"</c>, or <c>"asn_unknown"</c>.</returns>
    public static string GetAsnKey(HttpContext context, string[]? asnHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = asnHeaderCandidates is { Length: > 0 } ? asnHeaderCandidates : DefaultAsnHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"asn_{raw.ToUpperInvariant()}";
            }
        }

        return "asn_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the client's Autonomous System Number (ASN) with a client partition key.
    /// Isolates rate limits per client within specific ASNs.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="asnHeaderCandidates">Optional prioritized list of header names to search for ASN.</param>
    /// <returns>A compound key formatted as <c>"{asnKey}:{clientKey}"</c>.</returns>
    public static string GetAsnAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null, string[]? asnHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var asnKey = GetAsnKey(context, asnHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{asnKey}:{clientKey}";
    }

    private static readonly string[] DefaultTlsFingerprintHeaders =
    [
        "CF-JA4",                   // Cloudflare JA4
        "CF-JA3-Hash",              // Cloudflare JA3
        "X-SSL-JA3-Fingerprint",    // Envoy / Nginx TLS module
        "X-TLS-Client-Fingerprint", // AWS CloudFront / WAF
        "SSL-Client-Fingerprint"    // Generic reverse proxies
    ];

    /// <summary>
    /// Resolves a partition key based on the client's TLS ClientHello fingerprint (JA3 / JA4 hash) injected by an edge proxy.
    /// Defeats impersonation attacks where scripts (e.g. Python requests, curl) spoof browser User-Agent headers,
    /// because TLS cipher suite ordering and extensions are cryptographically tied to the client runtime.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="fingerprintHeaderCandidates">Optional prioritized list of header names to search for TLS fingerprint.</param>
    /// <returns>The TLS fingerprint key formatted as <c>"tlsfp_{hash}"</c>, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetTlsFingerprintKey(HttpContext context, string[]? fingerprintHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = fingerprintHeaderCandidates is { Length: > 0 } ? fingerprintHeaderCandidates : DefaultTlsFingerprintHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"tlsfp_{raw.ToLowerInvariant()}";
            }
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on an upstream WAF's bot classification score or category (such as <c>"CF-Bot-Score"</c> or <c>"X-WAF-Bot-Category"</c>).
    /// Segregates requests into automated, suspicious, or human categories to enforce tiered throttling.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="scoreHeader">The bot score header name to inspect. Defaults to <c>"CF-Bot-Score"</c>.</param>
    /// <returns>A categorized partition key: <c>"botscore_automated"</c>, <c>"botscore_suspicious"</c>, <c>"botscore_verified"</c>, or custom category.</returns>
    public static string GetBotThreatScoreKey(HttpContext context, string scoreHeader = "CF-Bot-Score")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scoreHeader);

        if (context.Request.Headers.TryGetValue(scoreHeader, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim();
            if (int.TryParse(raw, CultureInfo.InvariantCulture, out var score))
            {
                if (score < 30)
                    return "botscore_automated";
                if (score < 80)
                    return "botscore_suspicious";
                return "botscore_verified";
            }

            return $"botscore_{raw.ToLowerInvariant()}";
        }

        // Secondary fallback to X-WAF-Bot-Category
        if (context.Request.Headers.TryGetValue("X-WAF-Bot-Category", out var catVal) && !string.IsNullOrWhiteSpace(catVal))
        {
            return $"botscore_{catVal.ToString().Trim().ToLowerInvariant()}";
        }

        return "botscore_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the WAF bot threat score classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="scoreHeader">The bot score header name to inspect. Defaults to <c>"CF-Bot-Score"</c>.</param>
    /// <returns>A compound key formatted as <c>"{botThreatScoreKey}:{clientKey}"</c>.</returns>
    public static string GetBotThreatScoreAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null, string scoreHeader = "CF-Bot-Score")
    {
        ArgumentNullException.ThrowIfNull(context);

        var scoreKey = GetBotThreatScoreKey(context, scoreHeader);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{scoreKey}:{clientKey}";
    }

    private static readonly string[] DefaultCityHeaders =
    [
        "CF-IPCity",                // Cloudflare
        "CloudFront-Viewer-City",   // AWS CloudFront
        "X-Client-City",            // Reverse proxy
        "X-City"
    ];

    /// <summary>
    /// Resolves a partition key based on the client's metropolitan city location extracted from edge CDN headers (e.g. <c>"CF-IPCity"</c> or <c>"CloudFront-Viewer-City"</c>).
    /// Enables hyper-localized surge damping for on-demand logistics, ride-hailing, or localized event ticket releases.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="cityHeaderCandidates">Optional prioritized list of header names to search for city.</param>
    /// <returns>The city partition key formatted as <c>"city_{cityName}"</c>, or <c>"city_unknown"</c>.</returns>
    public static string GetGeoCityKey(HttpContext context, string[]? cityHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = cityHeaderCandidates is { Length: > 0 } ? cityHeaderCandidates : DefaultCityHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"city_{raw}";
            }
        }

        return "city_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the client's geographic city with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="cityHeaderCandidates">Optional prioritized list of header names to search for city.</param>
    /// <returns>A compound key formatted as <c>"{cityKey}:{clientKey}"</c>.</returns>
    public static string GetGeoCityAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null, string[]? cityHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cityKey = GetGeoCityKey(context, cityHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{cityKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on TLS 1.3 0-RTT Early Data headers (<c>"earlydata_0rtt"</c> vs <c>"earlydata_standard"</c>).
    /// Throttles 0-RTT requests to defend against network replay vulnerabilities inherent to 0-RTT handshakes.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"earlydata_0rtt"</c> if <c>Early-Data: 1</c> is present; otherwise <c>"earlydata_standard"</c>.</returns>
    public static string GetEarlyDataKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Early-Data", out var edVal) && edVal.ToString().Trim() == "1")
            return "earlydata_0rtt";

        return "earlydata_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the TLS 1.3 Early Data status with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{earlyDataKey}:{clientKey}"</c>.</returns>
    public static string GetEarlyDataAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var edKey = GetEarlyDataKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{edKey}:{clientKey}";
    }

    private static readonly string[] DefaultIpTypeHeaders =
    [
        "CF-IP-Type",
        "X-IP-Classification",
        "X-Client-IP-Type"
    ];

    /// <summary>
    /// Resolves a partition key based on IP intelligence network classification (<c>"iptype_datacenter"</c>, <c>"iptype_tor"</c>, <c>"iptype_vpn"</c>, <c>"iptype_residential"</c>, <c>"iptype_mobile"</c>).
    /// Enables aggressive throttling on cloud hosting, VPNs, and Tor exit nodes while providing generous quotas for residential and mobile devices.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="typeHeaderCandidates">Optional prioritized list of header names to search for IP classification.</param>
    /// <returns>The IP type partition key formatted as <c>"iptype_{type}"</c>, or <c>"iptype_unknown"</c>.</returns>
    public static string GetIpTypeKey(HttpContext context, string[]? typeHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = typeHeaderCandidates is { Length: > 0 } ? typeHeaderCandidates : DefaultIpTypeHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"iptype_{raw}";
            }
        }

        return "iptype_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the IP intelligence classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="typeHeaderCandidates">Optional prioritized list of header names to search for IP classification.</param>
    /// <returns>A compound key formatted as <c>"{ipTypeKey}:{clientKey}"</c>.</returns>
    public static string GetIpTypeAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? typeHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ipTypeKey = GetIpTypeKey(context, typeHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{ipTypeKey}:{clientKey}";
    }

    private static readonly string[] DefaultContinentHeaders =
    [
        "CF-Continent",
        "CloudFront-Viewer-Continent",
        "X-Continent-Code"
    ];

    /// <summary>
    /// Resolves a partition key based on the client's geographic continent code (e.g. <c>"NA"</c>, <c>"EU"</c>, <c>"AS"</c>, <c>"AF"</c>, <c>"SA"</c>, <c>"OC"</c>).
    /// Enables continent-level macro traffic damping during regional submarine cable cuts or geopolitical routing events.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="continentHeaderCandidates">Optional prioritized list of header names to search for continent code.</param>
    /// <returns>The continent partition key formatted as <c>"continent_{code}"</c>, or <c>"continent_unknown"</c>.</returns>
    public static string GetGeoContinentKey(HttpContext context, string[]? continentHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = continentHeaderCandidates is { Length: > 0 } ? continentHeaderCandidates : DefaultContinentHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"continent_{raw}";
            }
        }

        return "continent_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the geographic continent code with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="continentHeaderCandidates">Optional prioritized list of header names to search for continent code.</param>
    /// <returns>A compound key formatted as <c>"{continentKey}:{clientKey}"</c>.</returns>
    public static string GetGeoContinentAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? continentHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var contKey = GetGeoContinentKey(context, continentHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{contKey}:{clientKey}";
    }

    private static readonly string[] DefaultTlsVersionHeaders =
    [
        "X-SSL-Protocol",
        "SSL_PROTOCOL",
        "X-Forwarded-Tls-Version",
        "CF-Visitor"
    ];

    /// <summary>
    /// Resolves a partition key based on the TLS protocol version negotiated during handshake (<c>"tls_1_3"</c>, <c>"tls_1_2"</c>, <c>"tls_legacy"</c>, or <c>"tls_none"</c>).
    /// Inspects server TLS handshake features or edge CDN headers, allowing gateway operators to throttle legacy TLS versions.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="tlsHeaderCandidates">Optional prioritized list of header names to inspect if terminating behind a reverse proxy/CDN.</param>
    /// <returns>The TLS version partition key.</returns>
    public static string GetTlsProtocolVersionKey(HttpContext context, string[]? tlsHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Direct Kestrel / server TLS handshake feature inspection
        foreach (var feature in context.Features)
        {
            if (feature.Key.Name.Contains("TlsHandshake", StringComparison.OrdinalIgnoreCase) ||
                feature.Value?.GetType().Name.Contains("TlsHandshake", StringComparison.OrdinalIgnoreCase) == true)
            {
                var protocolProp = feature.Value?.GetType().GetProperty("Protocol");
                if (protocolProp is not null)
                {
                    var protoVal = protocolProp.GetValue(feature.Value)?.ToString();
                    if (!string.IsNullOrWhiteSpace(protoVal))
                    {
                        var lower = protoVal.ToLowerInvariant();
                        if (lower.Contains("13") || lower.Contains("1_3") || lower.Contains("1.3"))
                            return "tls_1_3";
                        if (lower.Contains("12") || lower.Contains("1_2") || lower.Contains("1.2"))
                            return "tls_1_2";
                        if (!lower.Contains("none"))
                            return "tls_legacy";
                    }
                }
            }
        }

        // 2. Fallback to reverse proxy / CDN headers
        var candidates = tlsHeaderCandidates is { Length: > 0 } ? tlsHeaderCandidates : DefaultTlsVersionHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (raw.Contains("tls 1.3") || raw.Contains("tls1.3") || raw.Contains("tls1_3") || raw.Contains("tlsv1.3"))
                    return "tls_1_3";
                if (raw.Contains("tls 1.2") || raw.Contains("tls1.2") || raw.Contains("tls1_2") || raw.Contains("tlsv1.2"))
                    return "tls_1_2";
                if (raw.Contains("tls 1.1") || raw.Contains("tls 1.0") || raw.Contains("ssl"))
                    return "tls_legacy";
            }
        }

        return context.Request.IsHttps ? "tls_unknown" : "tls_none";
    }

    /// <summary>
    /// Resolves a compound partition key combining the TLS protocol version with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="tlsHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{tlsKey}:{clientKey}"</c>.</returns>
    public static string GetTlsProtocolVersionAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? tlsHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tlsKey = GetTlsProtocolVersionKey(context, tlsHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{tlsKey}:{clientKey}";
    }

    private static readonly string[] DefaultAsOrgHeaders =
    [
        "CF-IP-Organization",
        "X-AS-Name",
        "X-Geo-ISP",
        "X-Autonomous-System-Organization"
    ];

    /// <summary>
    /// Resolves a partition key based on the client's Autonomous System Organization or Internet Service Provider (e.g. <c>"asorg_google"</c>, <c>"asorg_amazon"</c>, <c>"asorg_comcast"</c>).
    /// Injected by CDNs (such as Cloudflare or Fastly), allowing enterprise gateways to protect APIs against coordinated scrapes originating from specific hosting providers.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="asOrgHeaderCandidates">Optional prioritized list of header names to search.</param>
    /// <returns>The AS Organization partition key formatted as <c>"asorg_{name}"</c>, or <c>"asorg_unknown"</c>.</returns>
    public static string GetAsOrganizationKey(HttpContext context, string[]? asOrgHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = asOrgHeaderCandidates is { Length: > 0 } ? asOrgHeaderCandidates : DefaultAsOrgHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var sb = new StringBuilder("asorg_");
                    foreach (var ch in raw)
                    {
                        if (char.IsLetterOrDigit(ch))
                            sb.Append(char.ToLowerInvariant(ch));
                        else if (sb[^1] != '_')
                            sb.Append('_');
                    }

                    var result = sb.ToString().TrimEnd('_');
                    if (result.Length > 6)
                        return result;
                }
            }
        }

        return "asorg_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the AS Organization with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="asOrgHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{asOrgKey}:{clientKey}"</c>.</returns>
    public static string GetAsOrganizationAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? asOrgHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var asOrgKey = GetAsOrganizationKey(context, asOrgHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{asOrgKey}:{clientKey}";
    }

    private static readonly string[] DefaultCdnPopHeaders =
    [
        "CF-Ray",
        "X-Amz-Cf-Pop",
        "X-Served-By",
        "X-CDN-POP"
    ];

    /// <summary>
    /// Resolves a partition key based on the CDN edge Point-of-Presence (PoP) airport code (e.g. <c>"pop_ams"</c>, <c>"pop_fra"</c>, <c>"pop_iad"</c>, <c>"pop_dxb"</c>).
    /// Inspects Cloudflare Ray, CloudFront PoP, or Fastly headers, enabling localized regional rate limiting during submarine cable cuts or localized DDoS events.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="popHeaderCandidates">Optional prioritized list of header names to search.</param>
    /// <returns>The CDN PoP partition key formatted as <c>"pop_{airportCode}"</c>, or <c>"pop_unknown"</c>.</returns>
    public static string GetCdnPopKey(HttpContext context, string[]? popHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = popHeaderCandidates is { Length: > 0 } ? popHeaderCandidates : DefaultCdnPopHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();

                var hyphenIdx = raw.LastIndexOf('-');
                if (hyphenIdx >= 0 && hyphenIdx < raw.Length - 1)
                {
                    var suffix = raw[(hyphenIdx + 1)..].Trim().ToLowerInvariant();
                    if (suffix.Length is >= 3 and <= 4 && suffix.All(char.IsLetterOrDigit))
                        return $"pop_{suffix}";
                }

                if (raw.Length >= 3 && char.IsLetter(raw[0]) && char.IsLetter(raw[1]) && char.IsLetter(raw[2]))
                {
                    var code = raw[..3].ToLowerInvariant();
                    return $"pop_{code}";
                }
            }
        }

        return "pop_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the CDN PoP with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="popHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{popKey}:{clientKey}"</c>.</returns>
    public static string GetCdnPopAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? popHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var popKey = GetCdnPopKey(context, popHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{popKey}:{clientKey}";
    }

    private static readonly string[] DefaultTimestampHeaders =
    [
        "X-Signature-Timestamp",
        "X-Timestamp",
        "Date"
    ];

    /// <summary>
    /// Resolves a partition key based on request timestamp skew (<c>"skew_normal"</c>, <c>"skew_excessive"</c>, or <c>"skew_missing"</c>).
    /// Protects webhook endpoints and signed APIs (AWS SigV4 / Stripe HMAC) from signature replay attacks and broken client NTP synchronization.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="maxSkewSeconds">The maximum allowable clock drift in seconds. Defaults to 300 (5 minutes).</param>
    /// <param name="timeProvider">Optional TimeProvider for testability. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="timestampHeaderCandidates">Optional prioritized list of header names to search.</param>
    /// <returns>The timestamp skew partition key.</returns>
    public static string GetTimestampSkewKey(
        HttpContext context,
        int maxSkewSeconds = 300,
        TimeProvider? timeProvider = null,
        string[]? timestampHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSkewSeconds);

        var candidates = timestampHeaderCandidates is { Length: > 0 } ? timestampHeaderCandidates : DefaultTimestampHeaders;
        var provider = timeProvider ?? TimeProvider.System;
        var nowEpoch = provider.GetUtcNow().ToUnixTimeSeconds();

        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim();
                if (long.TryParse(raw, CultureInfo.InvariantCulture, out var epoch))
                {
                    if (epoch > 100_000_000_000L)
                        epoch /= 1000;

                    var diff = Math.Abs(nowEpoch - epoch);
                    return diff <= maxSkewSeconds ? "skew_normal" : "skew_excessive";
                }

                if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
                {
                    var diff = Math.Abs(nowEpoch - dto.ToUnixTimeSeconds());
                    return diff <= maxSkewSeconds ? "skew_normal" : "skew_excessive";
                }
            }
        }

        return "skew_missing";
    }

    /// <summary>
    /// Resolves a compound partition key combining the timestamp skew status with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="maxSkewSeconds">Maximum allowable clock drift in seconds.</param>
    /// <param name="timeProvider">Optional TimeProvider.</param>
    /// <param name="timestampHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{skewKey}:{clientKey}"</c>.</returns>
    public static string GetTimestampSkewAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int maxSkewSeconds = 300,
        TimeProvider? timeProvider = null,
        string[]? timestampHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var skewKey = GetTimestampSkewKey(context, maxSkewSeconds, timeProvider, timestampHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{skewKey}:{clientKey}";
    }

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".so", ".dylib", ".sh", ".bat", ".cmd", ".php", ".ps1", ".py", ".vbs", ".bin"
    };

    private static readonly HashSet<string> ScriptableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsm", ".docm", ".svg", ".htm", ".html", ".js", ".mjs", ".xml"
    };

    /// <summary>
    /// Resolves a partition key based on the security risk classification of uploaded multipart file extensions (<c>"ext_executable"</c>, <c>"ext_scriptable"</c>, <c>"ext_standard"</c>, <c>"ext_none"</c>).
    /// Applies immediate throttling to zero-trust file upload vectors before files are buffered or processed by downstream storage.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The file extension risk partition key.</returns>
    public static string GetUploadedFileExtensionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.HasFormContentType)
            return "ext_none";

        try
        {
            var files = context.Request.Form.Files;
            if (files.Count == 0)
                return "ext_none";

            var hasScriptable = false;
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext))
                    continue;

                if (ExecutableExtensions.Contains(ext))
                    return "ext_executable";

                if (ScriptableExtensions.Contains(ext))
                    hasScriptable = true;
            }

            return hasScriptable ? "ext_scriptable" : "ext_standard";
        }
        catch
        {
            return "ext_none";
        }
    }

    /// <summary>
    /// Resolves a compound partition key combining the uploaded file extension risk classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{extKey}:{clientKey}"</c>.</returns>
    public static string GetUploadedFileExtensionAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var extKey = GetUploadedFileExtensionKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{extKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on client automation and header anomaly classification (<c>"client_automation"</c>, <c>"client_missing_ua"</c>, or <c>"client_browser"</c>).
    /// Inspects the User-Agent signature for automated scraping utilities (curl, python, postman, aiohttp) to enforce aggressive rate limits on automated tools.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The client anomaly partition key.</returns>
    public static string GetHeaderOrderingAnomalyKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Headers.TryGetValue("User-Agent", out var uaVal) || string.IsNullOrWhiteSpace(uaVal))
            return "client_missing_ua";

        var ua = uaVal.ToString().ToLowerInvariant();
        if (ua.Contains("curl") ||
            ua.Contains("python") ||
            ua.Contains("postman") ||
            ua.Contains("insomnia") ||
            ua.Contains("httpie") ||
            ua.Contains("wget") ||
            ua.Contains("aiohttp") ||
            ua.Contains("scpy"))
        {
            return "client_automation";
        }

        return "client_browser";
    }

    /// <summary>
    /// Resolves a compound partition key combining the client automation classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{anomalyKey}:{clientKey}"</c>.</returns>
    public static string GetHeaderOrderingAnomalyAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var anomalyKey = GetHeaderOrderingAnomalyKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{anomalyKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on TLS session resumption status (<c>"tls_resumed"</c> vs <c>"tls_full_handshake"</c>).
    /// Detects resumed TLS sessions via reverse proxy headers (<c>"X-SSL-Session-Reused"</c>, <c>"SSL_SESSION_REUSED"</c>).
    /// Protects crypto acceleration hardware and CPU from cryptographic handshake exhaustion attacks.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"tls_resumed"</c> if the TLS session was resumed; otherwise <c>"tls_full_handshake"</c>.</returns>
    public static string GetTlsResumptionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("X-SSL-Session-Reused", out var reusedVal) && !string.IsNullOrWhiteSpace(reusedVal))
        {
            var str = reusedVal.ToString().Trim();
            if (string.Equals(str, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "resumed", StringComparison.OrdinalIgnoreCase))
            {
                return "tls_resumed";
            }
        }

        if (context.Request.Headers.TryGetValue("SSL_SESSION_REUSED", out var sslReusedVal) && !string.IsNullOrWhiteSpace(sslReusedVal))
        {
            var str = sslReusedVal.ToString().Trim();
            if (string.Equals(str, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "resumed", StringComparison.OrdinalIgnoreCase))
            {
                return "tls_resumed";
            }
        }

        return "tls_full_handshake";
    }

    /// <summary>
    /// Resolves a compound partition key combining the TLS resumption status with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{resumptionKey}:{clientKey}"</c>.</returns>
    public static string GetTlsResumptionAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resumptionKey = GetTlsResumptionKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{resumptionKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on Cloud WAF IP threat score ratings (<c>"threat_clean"</c>, <c>"threat_low"</c>, <c>"threat_elevated"</c>, <c>"threat_critical"</c>).
    /// Inspects industry standard WAF headers (<c>"CF-Threat-Score"</c>, <c>"X-Amzn-Waf-Threat-Score"</c>, <c>"X-Akamai-Threat-Score"</c>, <c>"X-Threat-Score"</c>).
    /// Automatically chokes high-threat IPs while preserving unhindered bandwidth for clean enterprise traffic.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The IP threat reputation partition key.</returns>
    public static string GetIpReputationScoreKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        int score = 0;
        if (context.Request.Headers.TryGetValue("CF-Threat-Score", out var cfThreat) && int.TryParse(cfThreat, out var cfScore))
            score = cfScore;
        else if (context.Request.Headers.TryGetValue("X-Amzn-Waf-Threat-Score", out var awsThreat) && int.TryParse(awsThreat, out var awsScore))
            score = awsScore;
        else if (context.Request.Headers.TryGetValue("X-Akamai-Threat-Score", out var akThreat) && int.TryParse(akThreat, out var akScore))
            score = akScore;
        else if (context.Request.Headers.TryGetValue("X-Threat-Score", out var genThreat) && int.TryParse(genThreat, out var genScore))
            score = genScore;

        if (score < 15)
            return "threat_clean";
        if (score < 50)
            return "threat_low";
        if (score < 80)
            return "threat_elevated";

        return "threat_critical";
    }

    /// <summary>
    /// Resolves a compound partition key combining the IP threat reputation score with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{threatKey}:{clientKey}"</c>.</returns>
    public static string GetIpReputationScoreAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var threatKey = GetIpReputationScoreKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{threatKey}:{clientKey}";
    }
}
