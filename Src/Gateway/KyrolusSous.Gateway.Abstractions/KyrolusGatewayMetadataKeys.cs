namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed constant metadata keys used across Kyrolus Gateway components,
/// fluent route builders, and transform providers to prevent magic strings and runtime typos.
/// </summary>
public static class KyrolusGatewayMetadataKeys
{
    /// <summary>
    /// Common namespace prefix for all Kyrolus Gateway metadata keys to prevent collisions with user or third-party metadata.
    /// </summary>
    public const string Prefix = "Kyrolus:";

    /// <summary>
    /// Metadata keys for IP filtering and network CIDR blocklist/allowlist transforms.
    /// </summary>
    public static class IpFilter
    {
        /// <summary>
        /// Comma-separated list of allowed IPv4/IPv6 addresses or CIDR blocks (e.g., <c>"192.168.1.0/24, 10.0.0.1"</c>).
        /// If specified, only matching client IPs are granted access (Fail-Closed).
        /// </summary>
        public const string Allowed = $"{Prefix}IpFilter:Allowed";

        /// <summary>
        /// Comma-separated list of explicitly blocked IPv4/IPv6 addresses or CIDR blocks (e.g., <c>"203.0.113.50, 198.51.100.0/24"</c>).
        /// Callers from these IPs are rejected with HTTP 403 Forbidden.
        /// </summary>
        public const string Blocked = $"{Prefix}IpFilter:Blocked";
    }

    /// <summary>
    /// Metadata keys for multi-tenancy enforcement.
    /// </summary>
    public static class Tenant
    {
        /// <summary>
        /// Indicates whether the route strictly requires an authenticated or resolved multi-tenant context (<c>"true"</c> or <c>"false"</c>).
        /// If <c>true</c> and no tenant is resolved, the gateway rejects the request with HTTP 400 Bad Request.
        /// </summary>
        public const string Required = $"{Prefix}Tenant:Required";
    }

    /// <summary>
    /// Metadata keys for request payload Content-Type MIME filtering transforms.
    /// </summary>
    public static class ContentType
    {
        /// <summary>
        /// Comma-separated list of acceptable request body MIME types (e.g., <c>"application/json, text/plain"</c>).
        /// Defends against unexpected payload deserialization and XXE attacks by rejecting unsupported types with HTTP 415.
        /// </summary>
        public const string Allowed = $"{Prefix}ContentType:Allowed";
    }

    /// <summary>
    /// Metadata keys for request header limits (DoS and buffer overflow protection).
    /// </summary>
    public static class Headers
    {
        /// <summary>
        /// Maximum number of HTTP headers allowed in an incoming request.
        /// Defends against Slowloris and header flood DoS attacks (rejected with HTTP 431).
        /// </summary>
        public const string MaxCount = $"{Prefix}Headers:MaxCount";

        /// <summary>
        /// Maximum combined byte length of all header names and values.
        /// Defends against buffer overflow and memory exhaustion (rejected with HTTP 431).
        /// </summary>
        public const string MaxTotalLength = $"{Prefix}Headers:MaxTotalLength";
    }

    /// <summary>
    /// Metadata keys for payload body size enforcement (HTTP 413 defense).
    /// </summary>
    public static class Payload
    {
        /// <summary>
        /// Maximum request body size in bytes (e.g., <c>"10485760"</c> for 10 MB).
        /// Enforced early at the gateway edge to reject oversized uploads with HTTP 413 Payload Too Large.
        /// </summary>
        public const string MaxSize = $"{Prefix}Payload:MaxSize";
    }

    /// <summary>
    /// Metadata keys for response buffering and streaming bypass.
    /// </summary>
    public static class Streaming
    {
        /// <summary>
        /// When set to <c>"true"</c>, disables response buffering and extends request activity timeouts.
        /// Essential for real-time WebSocket, Server-Sent Events (SSE), and gRPC streaming routes.
        /// </summary>
        public const string Enabled = $"{Prefix}Streaming:Enabled";
    }

    /// <summary>
    /// Metadata keys for HTTP Verb / Method security controls.
    /// In HTTP, "Verbs" refer to request methods (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public static class Verbs
    {
        /// <summary>
        /// When set to <c>"true"</c>, automatically blocks dangerous HTTP verbs (<c>TRACE</c>, <c>TRACK</c>, <c>CONNECT</c>).
        /// <para>
        /// <b>Security Rationale:</b><br/>
        /// - <c>TRACE</c> / <c>TRACK</c>: Defends against Cross-Site Tracing (XST - CWE-693) where attackers steal <c>HttpOnly</c> cookies.<br/>
        /// - <c>CONNECT</c>: Prevents unauthorized TCP proxy tunneling through the gateway.
        /// </para>
        /// </summary>
        public const string BlockDangerous = $"{Prefix}Verbs:BlockDangerous";
    }

    /// <summary>
    /// Metadata keys for gateway telemetry header suppression.
    /// </summary>
    public static class Telemetry
    {
        /// <summary>
        /// When set to <c>"true"</c>, suppresses the injection of the <c>X-Kyrolus-Gateway: Active</c> response header.
        /// Defends against Information Disclosure (CWE-200) by hiding internal gateway implementation details from external clients.
        /// </summary>
        public const string SuppressHeader = $"{Prefix}SuppressTelemetryHeader";
    }

    /// <summary>
    /// Metadata keys for HTTP method override (X-HTTP-Method-Override) spoofing defense.
    /// </summary>
    public static class MethodOverride
    {
        /// <summary>
        /// Controls whether method override headers (<c>X-HTTP-Method-Override</c>, <c>X-HTTP-Method</c>) are permitted (<c>"true"</c> or <c>"false"</c>).
        /// Defends against HTTP Verb Tampering (CWE-287 / CWE-654) by ensuring clients cannot silently bypass verb restrictions.
        /// </summary>
        public const string Allowed = $"{Prefix}MethodOverride:Allowed";
    }

    /// <summary>
    /// Metadata keys for mTLS client certificate header forwarding.
    /// </summary>
    public static class ClientCert
    {
        /// <summary>
        /// When set to <c>"true"</c>, forwards verified mTLS client certificate details (Thumbprint, Subject, Issuer) to backend microservices.
        /// Defends against Client Certificate Spoofing (CWE-295) by first stripping any untrusted client-supplied certificate headers.
        /// </summary>
        public const string Forward = $"{Prefix}ClientCert:Forward";
    }

    /// <summary>
    /// Metadata keys for edge HTTP response security headers.
    /// Injected on responses to instruct client web browsers to enforce modern browser-side security protections.
    /// </summary>
    public static class SecurityHeaders
    {
        /// <summary>
        /// Injects a custom <c>Content-Security-Policy</c> (CSP) header.
        /// Defends against Cross-Site Scripting (XSS - CWE-79) and data injection by specifying trusted sources of scripts, styles, and images.
        /// </summary>
        public const string Csp = $"{Prefix}SecurityHeaders:CSP";

        /// <summary>
        /// Injects the <c>X-Frame-Options</c> header (e.g., <c>"DENY"</c> or <c>"SAMEORIGIN"</c>).
        /// Defends against Clickjacking (UI Redressing) by preventing malicious websites from embedding this route in an <c>&lt;iframe&gt;</c>.
        /// </summary>
        public const string FrameOptions = $"{Prefix}SecurityHeaders:FrameOptions";

        /// <summary>
        /// Injects the <c>Referrer-Policy</c> header (e.g., <c>"no-referrer"</c>, <c>"strict-origin-when-cross-origin"</c>).
        /// Prevents leaking sensitive URL paths and query tokens to third-party domains when users navigate away.
        /// </summary>
        public const string ReferrerPolicy = $"{Prefix}SecurityHeaders:ReferrerPolicy";

        /// <summary>
        /// Injects the <c>Strict-Transport-Security</c> (HSTS) header (e.g., <c>"max-age=63072000; includeSubDomains; preload"</c>).
        /// Defends against SSL Stripping and Man-In-The-Middle (MitM) attacks by forcing browsers to communicate strictly over HTTPS.
        /// </summary>
        public const string Hsts = $"{Prefix}SecurityHeaders:HSTS";
    }
}
