namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Configuration options for the outbound HTTP client used by the Gateway to communicate with backend cluster destinations.
/// </summary>
public sealed record KyrolusHttpClientOptions
{
    /// <summary>
    /// Gets a value indicating whether to bypass SSL certificate validation for backend destinations.
    /// WARNING: Enable ONLY in local development, testing, or internal isolated Docker environments with self-signed certificates.
    /// Never enable in untrusted production networks. Defaults to <c>false</c>.
    /// </summary>
    public bool DangerousAcceptAnyServerCertificate { get; init; } = false;

    /// <summary>
    /// Gets the maximum number of concurrent HTTP/1.1 connections allowed per destination server.
    /// Defends against socket exhaustion and resource starvation.
    /// </summary>
    public int? MaxConnectionsPerServer { get; init; }

    /// <summary>
    /// Gets a value indicating whether multiple HTTP/2 connections to the same server are permitted.
    /// Useful for high-throughput gRPC and HTTP/2 microservices.
    /// </summary>
    public bool? EnableMultipleHttp2Connections { get; init; }

    /// <summary>
    /// Gets the default HTTP protocol version to use for outbound requests to this cluster (e.g. <c>HttpVersion.Version20</c> for gRPC or <c>HttpVersion.Version30</c>).
    /// </summary>
    public Version? DefaultVersion { get; init; }

    /// <summary>
    /// Gets the version policy for negotiating HTTP protocols (e.g. <c>HttpVersionPolicy.RequestVersionExact</c> for strict gRPC).
    /// </summary>
    public HttpVersionPolicy? VersionPolicy { get; init; }
}
