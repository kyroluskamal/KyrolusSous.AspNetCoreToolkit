using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.RateLimiting;

/// <summary>
/// Cloud-Native RPC, gRPC Services, Streaming Modes, and gRPC Timeout Deadline Budget partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a partition key based on the gRPC Service and Method extracted from the request path (RFC gRPC over HTTP/2).
    /// For instance, <c>"/orders.OrderService/CreateOrder"</c> resolves to <c>"grpc_orderservice_createorder"</c>.
    /// Isolates rate limiting quotas across individual RPC methods within microservice clusters.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The gRPC service and method partition key, or <c>"grpc_non_rpc"</c>.</returns>
    public static string GetGrpcServiceAndMethodKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var path = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path) || path.Length < 3 || path[0] != '/')
            return "grpc_non_rpc";

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            var rawService = segments[0];
            var dotIdx = rawService.LastIndexOf('.');
            var serviceName = dotIdx >= 0 && dotIdx < rawService.Length - 1 ? rawService[(dotIdx + 1)..] : rawService;
            var methodName = segments[1];

            return $"grpc_{SanitizeSegment(serviceName)}_{SanitizeSegment(methodName)}";
        }

        return "grpc_non_rpc";
    }

    /// <summary>
    /// Resolves a compound partition key combining the gRPC service and method with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{grpcMethodKey}:{clientKey}"</c>.</returns>
    public static string GetGrpcServiceAndMethodAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var methodKey = GetGrpcServiceAndMethodKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{methodKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the gRPC RPC execution call pattern (<c>"grpc_unary"</c>, <c>"grpc_server_streaming"</c>, <c>"grpc_client_streaming"</c>, <c>"grpc_bidi_streaming"</c>).
    /// Protects gateway HTTP/2 streams and socket threadpools from long-lived bidirectional streaming RPC starvation.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The gRPC call pattern partition key.</returns>
    public static string GetGrpcCallTypeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("X-Grpc-Call-Type", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (raw.Contains("bidi")) return "grpc_bidi_streaming";
            if (raw.Contains("client")) return "grpc_client_streaming";
            if (raw.Contains("server")) return "grpc_server_streaming";
            if (raw.Contains("unary")) return "grpc_unary";
        }

        var ct = context.Request.ContentType;
        if (ct is not null && ct.Contains("application/grpc", StringComparison.OrdinalIgnoreCase))
            return "grpc_unary";

        return "grpc_call_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the gRPC call pattern with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{callTypeKey}:{clientKey}"</c>.</returns>
    public static string GetGrpcCallTypeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var callKey = GetGrpcCallTypeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{callKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the gRPC <c>"grpc-timeout"</c> deadline budget header (<c>"grpc_deadline_tight"</c>, <c>"grpc_deadline_standard"</c>, <c>"grpc_deadline_loose"</c>).
    /// Defends upstream gRPC microservices against slow-read connection pool exhaustion by isolating requests with exceptionally long deadlines.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="tightTimeoutSeconds">Threshold in seconds for tight deadlines. Defaults to 1 second.</param>
    /// <param name="looseTimeoutSeconds">Threshold in seconds above which a deadline is categorized as loose. Defaults to 60 seconds.</param>
    /// <returns>The gRPC deadline budget partition key.</returns>
    public static string GetGrpcDeadlineBudgetBucketKey(
        HttpContext context,
        int tightTimeoutSeconds = 1,
        int looseTimeoutSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tightTimeoutSeconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(looseTimeoutSeconds, tightTimeoutSeconds);

        if (context.Request.Headers.TryGetValue("grpc-timeout", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim();
            if (raw.Length >= 2)
            {
                var unit = raw[^1];
                var numberPart = raw[..^1];
                if (double.TryParse(numberPart, CultureInfo.InvariantCulture, out var timeoutValue))
                {
                    var seconds = unit switch
                    {
                        'H' => timeoutValue * 3600,
                        'M' => timeoutValue * 60,
                        'S' => timeoutValue,
                        'm' => timeoutValue / 1000.0,
                        'u' => timeoutValue / 1000000.0,
                        'n' => timeoutValue / 1000000000.0,
                        _ => timeoutValue
                    };

                    if (seconds <= tightTimeoutSeconds)
                        return "grpc_deadline_tight";
                    if (seconds <= looseTimeoutSeconds)
                        return "grpc_deadline_standard";

                    return "grpc_deadline_loose";
                }
            }
        }

        return "grpc_deadline_none";
    }

    /// <summary>
    /// Resolves a compound partition key combining the gRPC deadline budget with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="tightTimeoutSeconds">Threshold in seconds for tight deadlines.</param>
    /// <param name="looseTimeoutSeconds">Threshold in seconds for loose deadlines.</param>
    /// <returns>A compound key formatted as <c>"{deadlineKey}:{clientKey}"</c>.</returns>
    public static string GetGrpcDeadlineBudgetBucketAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int tightTimeoutSeconds = 1,
        int looseTimeoutSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deadlineKey = GetGrpcDeadlineBudgetBucketKey(context, tightTimeoutSeconds, looseTimeoutSeconds);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{deadlineKey}:{clientKey}";
    }
}
