namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// Backward-compatible provider for gateway telemetry headers (<c>X-Kyrolus-Gateway: Active</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Architecture:</b><br/>
/// Inherits from <see cref="KyrolusTelemetryHeadersTransformProvider"/> to preserve compatibility with existing configurations.
/// For actual rate limiting, use <see cref="KyrolusGatewayRoute.RateLimiterPolicy"/>.
/// </para>
/// </remarks>
public sealed class KyrolusRateLimitTransformProvider : KyrolusTelemetryHeadersTransformProvider
{
}
