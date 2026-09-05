using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Extracts tenant subdomain information from incoming host headers and injects X-Tenant-ID.
/// </summary>
public sealed class KyrolusTenantRoutingTransformProvider : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var host = transformContext.HttpContext.Request.Host.Host;
            var parts = host.Split('.');
            if (parts.Length > 2)
            {
                var tenantSubdomain = parts[0];
                transformContext.ProxyRequest.Headers.Remove("X-Tenant-ID");
                transformContext.ProxyRequest.Headers.Add("X-Tenant-ID", tenantSubdomain);
            }
            return ValueTask.CompletedTask;
        });
    }
}
