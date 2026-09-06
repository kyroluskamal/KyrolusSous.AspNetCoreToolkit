namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Configuration options for edge IP address filtering (allowlist and blocklist) on gateway routes.
/// </summary>
public sealed record KyrolusIpFilterOptions
{
    /// <summary>
    /// Gets the list of allowed IPv4/IPv6 addresses or CIDR blocks (e.g. <c>"192.168.1.50"</c>, <c>"10.0.0.0/8"</c>).
    /// If non-empty, any client IP not matching an allowed entry is immediately rejected with HTTP 403 Forbidden.
    /// </summary>
    public IReadOnlyList<string>? AllowedIpsOrCidrs { get; init; }

    /// <summary>
    /// Gets the list of blocked IPv4/IPv6 addresses or CIDR blocks (e.g. <c>"203.0.113.195"</c>, <c>"198.51.100.0/24"</c>).
    /// Any client IP matching a blocked entry is immediately rejected with HTTP 403 Forbidden.
    /// </summary>
    public IReadOnlyList<string>? BlockedIpsOrCidrs { get; init; }
}
