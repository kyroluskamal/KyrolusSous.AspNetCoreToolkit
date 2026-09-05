using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Injects gateway telemetry and status headers into reverse proxy HTTP responses.
/// </summary>
public sealed class KyrolusRateLimitTransformProvider : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddResponseTransform(transformContext =>
        {
            transformContext.HttpContext.Response.Headers["X-Kyrolus-Gateway"] = "Active";
            return ValueTask.CompletedTask;
        });
    }
}
