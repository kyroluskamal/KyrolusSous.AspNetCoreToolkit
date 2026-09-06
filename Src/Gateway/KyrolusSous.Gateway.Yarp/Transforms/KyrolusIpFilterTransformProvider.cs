namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that enforces edge IP address filtering (allowlist and blocklist) on gateway routes.
/// Evaluates incoming client remote IP addresses against route metadata rules and rejects unauthorized connections with HTTP 403 Forbidden.
/// </summary>
public sealed class KyrolusIpFilterTransformProvider : ITransformProvider
{
    private static readonly byte[] ForbiddenResponseBytes =
        """{"type":"https://httpstatuses.com/403","title":"Forbidden","status":403,"detail":"Access from your IP address is restricted."}"""u8.ToArray();

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        if (context.Route.Metadata is null)
        {
            return;
        }

        ValidateIpFilterEntries(context, "Kyrolus:IpFilter:Allowed");
        ValidateIpFilterEntries(context, "Kyrolus:IpFilter:Blocked");
    }

    private static void ValidateIpFilterEntries(TransformRouteValidationContext context, string metadataKey)
    {
        if (context.Route.Metadata?.TryGetValue(metadataKey, out var raw) == true && !string.IsNullOrWhiteSpace(raw))
        {
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (!IPNetwork.TryParse(part, out _) && !IPAddress.TryParse(part, out _))
                {
                    context.Errors.Add(new ArgumentException($"Route '{context.Route.RouteId}' has invalid IP or CIDR in metadata '{metadataKey}': '{part}'."));
                }
            }
        }
    }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the IP address filtering transform to the YARP transform pipeline if route metadata defines allowlist or blocklist rules.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;
        if (metadata is null)
        {
            return;
        }

        metadata.TryGetValue("Kyrolus:IpFilter:Allowed", out var allowedRaw);
        metadata.TryGetValue("Kyrolus:IpFilter:Blocked", out var blockedRaw);

        if (string.IsNullOrWhiteSpace(allowedRaw) && string.IsNullOrWhiteSpace(blockedRaw))
        {
            return;
        }

        var allowedList = ParseNetworks(allowedRaw);
        var blockedList = ParseNetworks(blockedRaw);

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var remoteIp = transformContext.HttpContext.Connection.RemoteIpAddress;
            if (IsRequestForbidden(remoteIp, allowedList, blockedList))
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                transformContext.HttpContext.Response.ContentType = "application/problem+json";
                await transformContext.HttpContext.Response.Body.WriteAsync(ForbiddenResponseBytes, transformContext.HttpContext.RequestAborted);
            }
        });
    }

    private static bool IsRequestForbidden(IPAddress? remoteIp, List<IPNetwork> allowedList, List<IPNetwork> blockedList)
    {
        if (remoteIp is null)
        {
            // Fail-Closed: If an allowlist is enforced, unidentifiable clients cannot be granted access
            return allowedList.Count > 0;
        }

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        // 1. Check if explicitly blocked
        if (blockedList.Count > 0 && MatchesAny(remoteIp, blockedList))
        {
            return true;
        }

        // 2. Check if allowlist is enforced and IP is not allowed
        if (allowedList.Count > 0 && !MatchesAny(remoteIp, allowedList))
        {
            return true;
        }

        return false;
    }

    private static List<IPNetwork> ParseNetworks(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var list = new List<IPNetwork>();
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (IPNetwork.TryParse(part, out var network))
            {
                list.Add(network);
            }
            else if (IPAddress.TryParse(part, out var ip))
            {
                var prefix = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
                list.Add(new IPNetwork(ip, prefix));
            }
        }

        return list;
    }

    private static bool MatchesAny(IPAddress ip, List<IPNetwork> networks) =>
        networks.Any(net => net.Contains(ip));
}
