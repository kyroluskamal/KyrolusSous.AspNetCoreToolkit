using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Applies or forwards the correlation ID header to upstream proxy requests.
/// </summary>
public sealed class KyrolusCorrelationTransformProvider : ITransformProvider
{
    private const string HeaderName = "X-Correlation-ID";

    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

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
