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
    private const string CorrelationContextItemKey = "KyrolusCorrelationId";

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the correlation ID request and response transforms to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return ValueTask.CompletedTask;
            }

            var rawHeader = transformContext.HttpContext.Request.Headers[HeaderName].ToString();
            if (!IsValidCorrelationId(rawHeader))
            {
                var requestId = transformContext.HttpContext.Request.Headers["X-Request-ID"].ToString();
                rawHeader = IsValidCorrelationId(requestId) ? requestId : null;
            }

            var correlationId = rawHeader
                ?? System.Diagnostics.Activity.Current?.TraceId.ToString()
                ?? Guid.NewGuid().ToString("N");

            transformContext.HttpContext.Items[CorrelationContextItemKey] = correlationId;

            transformContext.ProxyRequest.Headers.Remove(HeaderName);
            transformContext.ProxyRequest.Headers.Add(HeaderName, correlationId);
            return ValueTask.CompletedTask;
        });

        context.AddResponseTransform(transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return ValueTask.CompletedTask;
            }

            if (transformContext.HttpContext.Items.TryGetValue(CorrelationContextItemKey, out var val) && val is string correlationId)
            {
                transformContext.HttpContext.Response.Headers[HeaderName] = correlationId;
            }
            return ValueTask.CompletedTask;
        });
    }

    private static bool IsValidCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return false;
        }

        return value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
    }
}
