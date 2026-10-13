namespace KyrolusSous.Gateway.Yarp.Diagnostics;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance ASP.NET Core middleware that records structured access logs and OpenTelemetry metrics for requests traversing the API gateway.
/// Tracks real-time request counts (<c>gateway.requests.total</c>) and request duration histograms (<c>gateway.requests.duration_ms</c>).
/// </summary>
public sealed class KyrolusGatewayAccessLoggingMiddleware
{
    private static readonly Meter GatewayMeter = new("KyrolusSous.Gateway", "1.0.0");
    private static readonly Counter<long> RequestCounter = GatewayMeter.CreateCounter<long>(
        "gateway.requests.total",
        "requests",
        "Total number of HTTP requests processed by the API gateway.");
    private static readonly Histogram<double> DurationHistogram = GatewayMeter.CreateHistogram<double>(
        "gateway.requests.duration_ms",
        "ms",
        "Latency distribution of requests handled by the API gateway in milliseconds.");

    private readonly RequestDelegate _next;
    private readonly ILogger<KyrolusGatewayAccessLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusGatewayAccessLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public KyrolusGatewayAccessLoggingMiddleware(
        RequestDelegate next,
        ILogger<KyrolusGatewayAccessLoggingMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to time and record access logging and OpenTelemetry metrics for the request.
    /// </summary>
    /// <param name="context">The incoming HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            await _next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            RecordAccessAndMetrics(context, elapsedMs);
        }
    }

    private void RecordAccessAndMetrics(HttpContext context, double elapsedMs)
    {
        var request = context.Request;
        var response = context.Response;

        var proxyFeature = context.Features.Get<IReverseProxyFeature>();
        var routeId = proxyFeature?.Route?.Config.RouteId ?? "unknown";
        var clusterId = proxyFeature?.Cluster?.Config.ClusterId ?? "unknown";
        var destinationAddress = proxyFeature?.ProxiedDestination?.Model.Config.Address ?? "edge";

        var method = request.Method;
        var path = request.Path.Value ?? "/";
        var statusCode = response.StatusCode;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var corrHeader) && corrHeader.Count > 0
            ? corrHeader[0]
            : context.TraceIdentifier;

        // 1. Structured Access Log
        if (statusCode >= 500)
        {
            _logger.LogWarning(
                "Gateway {HttpMethod} {Path} -> Status: {StatusCode} | Duration: {DurationMs:F2}ms | Route: {RouteId} | Cluster: {ClusterId} | Destination: {DestinationAddress} | Client: {ClientIp} | Correlation: {CorrelationId}",
                method, path, statusCode, elapsedMs, routeId, clusterId, destinationAddress, clientIp, correlationId);
        }
        else
        {
            _logger.LogInformation(
                "Gateway {HttpMethod} {Path} -> Status: {StatusCode} | Duration: {DurationMs:F2}ms | Route: {RouteId} | Cluster: {ClusterId} | Destination: {DestinationAddress} | Client: {ClientIp} | Correlation: {CorrelationId}",
                method, path, statusCode, elapsedMs, routeId, clusterId, destinationAddress, clientIp, correlationId);
        }

        // 2. OpenTelemetry Metrics Recording
        var tags = new TagList
        {
            { "route_id", routeId },
            { "cluster_id", clusterId },
            { "http_status", statusCode },
            { "http_method", method }
        };

        RequestCounter.Add(1, tags);
        DurationHistogram.Record(elapsedMs, tags);
    }
}

/// <summary>
/// Pipeline extension methods for registering <see cref="KyrolusGatewayAccessLoggingMiddleware"/>.
/// </summary>
public static class KyrolusGatewayAccessLoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds structured access logging and OpenTelemetry metrics to the gateway HTTP request pipeline.
    /// </summary>
    /// <param name="app">The application pipeline builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseKyrolusGatewayAccessLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<KyrolusGatewayAccessLoggingMiddleware>();
    }
}
