namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that injects gateway telemetry and presence verification headers
/// (<c>X-Kyrolus-Gateway: Active</c>) into reverse proxy HTTP responses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Telemetry Role:</b><br/>
/// Injects an audit and proof-of-transit header informing downstream clients and monitoring probes that the response was
/// successfully routed through the KyrolusSous API Gateway security boundary.
/// </para>
/// </remarks>
public class KyrolusTelemetryHeadersTransformProvider : ITransformProvider
{
    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the gateway telemetry response transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;
        if (metadata != null &&
            metadata.TryGetValue("Kyrolus:SuppressTelemetryHeader", out var suppress) &&
            bool.TryParse(suppress, out var shouldSuppress) &&
            shouldSuppress)
        {
            return;
        }

        context.AddResponseTransform(transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return ValueTask.CompletedTask;
            }

            transformContext.HttpContext.Response.Headers["X-Kyrolus-Gateway"] = "Active";
            return ValueTask.CompletedTask;
        });
    }
}
