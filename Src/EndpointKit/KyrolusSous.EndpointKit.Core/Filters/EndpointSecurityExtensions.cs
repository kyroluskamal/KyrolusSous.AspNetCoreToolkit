using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.EndpointKit.Core.Filters;

/// <summary>
/// Fluent security extension methods for Minimal API endpoints in EndpointKit,
/// enabling per-endpoint payload size limits and header count/size limits.
/// </summary>
public static class EndpointSecurityExtensions
{
    /// <summary>
    /// Enforces a maximum allowable request payload size (in bytes) on this endpoint,
    /// returning HTTP 413 Payload Too Large early if the Content-Length exceeds this threshold.
    /// </summary>
    public static RouteHandlerBuilder WithMaxPayloadSize(this RouteHandlerBuilder builder, long maxSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEndpointFilter(new KyrolusPayloadSizeEndpointFilter(maxSizeBytes));
        return builder;
    }

    /// <summary>
    /// Enforces maximum allowable request header count and total size (in bytes) on this endpoint,
    /// returning HTTP 431 Request Header Fields Too Large if exceeded.
    /// </summary>
    public static RouteHandlerBuilder WithMaxHeaderLimits(
        this RouteHandlerBuilder builder,
        int maxHeaderCount,
        int maxTotalHeaderSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEndpointFilter(new KyrolusHeaderLimitsEndpointFilter(maxHeaderCount, maxTotalHeaderSizeBytes));
        return builder;
    }
}
