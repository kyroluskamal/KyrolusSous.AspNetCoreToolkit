namespace KyrolusSous.RateLimiting;

/// <summary>
/// Network, transport, IP addresses, subnets, and connection port partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a partition key based on the client's remote IP address.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The remote IP string, or <c>"unknown_ip"</c> if unavailable.</returns>
    public static string GetClientIpKey(HttpContext context) => GetClientIpKey(context, null);

    /// <summary>
    /// Resolves a partition key based on the client's remote IP address or a trusted forwarded header (e.g. <c>"CF-Connecting-IP"</c> or <c>"X-Forwarded-For"</c>).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="forwardedHeader">Optional custom header name to inspect for client IP before falling back to connection remote IP.</param>
    /// <returns>The remote IP string, or <c>"unknown_ip"</c> if unavailable.</returns>
    public static string GetClientIpKey(HttpContext context, string? forwardedHeader)
        => GetClientIpKey(context, forwardedHeader, isTrustedProxy: null);

    /// <summary>
    /// Resolves a partition key based on the client's remote IP address or a trusted forwarded header (e.g. <c>"CF-Connecting-IP"</c> or <c>"X-Forwarded-For"</c>),
    /// strictly requiring the direct connection remote IP to satisfy the <paramref name="isTrustedProxy"/> predicate before honoring the header.
    /// Defends against rate limiter bypass via client IP spoofing (CWE-345 / CWE-290).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="forwardedHeader">Optional custom header name to inspect for client IP.</param>
    /// <param name="isTrustedProxy">Optional predicate validating whether the immediate connecting remote IP is an authorized reverse proxy / CDN.</param>
    /// <returns>The resolved client IP string, or <c>"unknown_ip"</c> if unavailable.</returns>
    public static string GetClientIpKey(HttpContext context, string? forwardedHeader, Func<IPAddress, bool>? isTrustedProxy)
    {
        ArgumentNullException.ThrowIfNull(context);

        var remoteIp = context.Connection.RemoteIpAddress;
        var isProxyTrusted = isTrustedProxy is null || (remoteIp is not null && isTrustedProxy(remoteIp));

        if (isProxyTrusted &&
            !string.IsNullOrWhiteSpace(forwardedHeader) &&
            context.Request.Headers.TryGetValue(forwardedHeader, out var val) &&
            !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString();
            var commaIdx = raw.IndexOf(',');
            var ipStr = commaIdx >= 0 ? raw[..commaIdx].Trim() : raw.Trim();
            if (!string.IsNullOrWhiteSpace(ipStr))
                return ipStr;
        }

        return remoteIp?.ToString() ?? "unknown_ip";
    }

    /// <summary>
    /// Resolves a partition key based on the client IP's network subnet CIDR block (defaults to <c>/24</c> for IPv4 and <c>/64</c> for IPv6).
    /// Defends against distributed botnet attacks where attackers rotate adjacent IP addresses within the same ISP block.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="ipv4PrefixLength">The IPv4 subnet prefix length. Defaults to 24 (aggregates 256 addresses).</param>
    /// <param name="ipv6PrefixLength">The IPv6 subnet prefix length. Defaults to 64.</param>
    /// <returns>The subnet CIDR string (e.g. <c>"subnet_192.168.1.0/24"</c>), or <c>"unknown_ip"</c>.</returns>
    public static string GetSubnetKey(HttpContext context, int ipv4PrefixLength = 24, int ipv6PrefixLength = 64)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ipv4PrefixLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ipv4PrefixLength, 32);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ipv6PrefixLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ipv6PrefixLength, 128);

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null)
            return "unknown_ip";

        if (remoteIp.IsIPv4MappedToIPv6)
            remoteIp = remoteIp.MapToIPv4();

        if (remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var network = new IPNetwork(remoteIp, ipv4PrefixLength);
            return $"subnet_{network.BaseAddress}/{ipv4PrefixLength}";
        }

        if (remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var network = new IPNetwork(remoteIp, ipv6PrefixLength);
            return $"subnet_{network.BaseAddress}/{ipv6PrefixLength}";
        }

        return $"ip_{remoteIp}";
    }

    /// <summary>
    /// Resolves a partition key based on the physical TCP / Kestrel connection identifier (<see cref="ConnectionInfo.Id"/>).
    /// Defends against HTTP pipelining starvation and single-socket request floods.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The connection partition key formatted as <c>"conn_{connectionId}"</c>, or <c>"conn_unknown"</c>.</returns>
    public static string GetConnectionIdKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var connId = context.Connection.Id;
        if (string.IsNullOrWhiteSpace(connId))
            return "conn_unknown";

        return $"conn_{connId.Trim()}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the physical TCP connection identifier with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{connectionKey}:{clientKey}"</c>.</returns>
    public static string GetConnectionIdAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var connKey = GetConnectionIdKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{connKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the HTTP transport scheme (<c>"scheme_https"</c> vs <c>"scheme_http"</c>).
    /// Prevents plaintext HTTP traffic from consuming secure endpoint capacity by restricting cleartext quotas to redirection flows.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"scheme_https"</c> for TLS connections; otherwise <c>"scheme_http"</c>.</returns>
    public static string GetSchemeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.IsHttps ? "scheme_https" : "scheme_http";
    }

    /// <summary>
    /// Resolves a compound partition key combining the transport scheme with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{schemeKey}:{clientKey}"</c>.</returns>
    public static string GetSchemeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var schemeKey = GetSchemeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{schemeKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the local listening server port (<see cref="ConnectionInfo.LocalPort"/>).
    /// Separates rate limit quotas across multiple edge listeners (e.g. public port 443 vs partner mTLS port 8443 vs admin mesh port 9090).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The port partition key formatted as <c>"port_{localPort}"</c>, or <c>"port_unknown"</c>.</returns>
    public static string GetServerPortKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var port = context.Connection.LocalPort;
        return port > 0 ? $"port_{port}" : "port_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the local listening server port with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{portKey}:{clientKey}"</c>.</returns>
    public static string GetServerPortAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var portKey = GetServerPortKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{portKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the forwarded port via <c>X-Forwarded-Port</c>, falling back to the local connection port.
    /// Enables accurate port-based rate partitioning when operating behind external load balancers or Kubernetes Ingress controllers.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="forwardedPortHeader">The forwarded port header to inspect. Defaults to <c>"X-Forwarded-Port"</c>.</param>
    /// <returns>The forwarded port partition key formatted as <c>"fwdport_{port}"</c>, or <c>"fwdport_unknown"</c>.</returns>
    public static string GetForwardedPortKey(HttpContext context, string forwardedPortHeader = "X-Forwarded-Port")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(forwardedPortHeader);

        if (context.Request.Headers.TryGetValue(forwardedPortHeader, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim();
            var commaIdx = raw.IndexOf(',');
            var portStr = commaIdx >= 0 ? raw[..commaIdx].Trim() : raw;
            if (int.TryParse(portStr, CultureInfo.InvariantCulture, out var fwdPort) && fwdPort > 0)
                return $"fwdport_{fwdPort}";
        }

        var localPort = context.Connection.LocalPort;
        return localPort > 0 ? $"fwdport_{localPort}" : "fwdport_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the forwarded port with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="forwardedPortHeader">The forwarded port header to inspect.</param>
    /// <returns>A compound key formatted as <c>"{fwdPortKey}:{clientKey}"</c>.</returns>
    public static string GetForwardedPortAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string forwardedPortHeader = "X-Forwarded-Port")
    {
        ArgumentNullException.ThrowIfNull(context);

        var portKey = GetForwardedPortKey(context, forwardedPortHeader);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{portKey}:{clientKey}";
    }
}
