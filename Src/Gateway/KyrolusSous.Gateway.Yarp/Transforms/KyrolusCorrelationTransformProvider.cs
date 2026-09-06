namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that enforces distributed tracing by propagating or generating an <c>X-Correlation-ID</c> header on upstream proxy requests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Distributed Tracing Role:</b><br/>
/// Every HTTP request passing through the API Gateway requires a unique correlation identifier to track the request
/// across multiple downstream microservices and log aggregation systems (Seq, OpenTelemetry, Serilog).
/// If the inbound request already contains an <c>X-Correlation-ID</c>, it is preserved; otherwise, a new unique 32-character GUID is generated.
/// </para>
/// </remarks>
public sealed class KyrolusCorrelationTransformProvider : ITransformProvider
{
    private const string HeaderName = "X-Correlation-ID";

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the correlation ID transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            if (!transformContext.HttpContext.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
            }

            transformContext.ProxyRequest.Headers.Remove(HeaderName);
            transformContext.ProxyRequest.Headers.Add(HeaderName, correlationId.ToString());
            return ValueTask.CompletedTask;
        });
    }
}
