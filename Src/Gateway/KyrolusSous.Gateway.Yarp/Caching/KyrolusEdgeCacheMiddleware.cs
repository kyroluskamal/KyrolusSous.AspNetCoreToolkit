namespace KyrolusSous.Gateway.Yarp.Caching;

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using KyrolusSous.Gateway.Abstractions;

/// <summary>
/// High-performance edge response caching middleware for idempotent HTTP GET and HEAD requests.
/// Serves cached responses directly with <c>X-Kyrolus-Cache: HIT</c> or caches successful upstream responses (<c>X-Kyrolus-Cache: MISS</c>).
/// </summary>
public sealed class KyrolusEdgeCacheMiddleware
{
    private const string CacheHitHeaderValue = "HIT";
    private const string CacheMissHeaderValue = "MISS";
    private const string CacheHeaderName = "X-Kyrolus-Cache";

    private readonly RequestDelegate _next;
    private readonly IMemoryCache _memoryCache;

    private sealed record CachedResponseEntry(
        int StatusCode,
        string? ContentType,
        byte[] Body,
        Dictionary<string, string[]> Headers);

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusEdgeCacheMiddleware"/> class.
    /// </summary>
    public KyrolusEdgeCacheMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _next = next;
        _memoryCache = serviceProvider.GetService<IMemoryCache>() ?? new MemoryCache(new MemoryCacheOptions());
    }

    /// <summary>
    /// Executes the edge response caching logic.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var isCacheableMethod = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

        if (!isCacheableMethod)
        {
            await _next(context);
            return;
        }

        // Check if route has caching enabled in YARP Route metadata
        var proxyFeature = context.Features.Get<IReverseProxyFeature>();
        var metadata = proxyFeature?.Route?.Config.Metadata;

        if (metadata is null ||
            !metadata.TryGetValue(KyrolusGatewayMetadataKeys.Cache.Enabled, out var cacheEnabled) ||
            !string.Equals(cacheEnabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Compute Cache Key
        var cacheKey = BuildCacheKey(context, metadata);

        // 1. Check for Cache HIT
        if (_memoryCache.TryGetValue(cacheKey, out CachedResponseEntry? entry) && entry != null)
        {
            context.Response.Headers[CacheHeaderName] = CacheHitHeaderValue;
            context.Response.StatusCode = entry.StatusCode;

            if (!string.IsNullOrWhiteSpace(entry.ContentType))
            {
                context.Response.ContentType = entry.ContentType;
            }

            foreach (var (headerKey, headerValues) in entry.Headers)
            {
                if (!string.Equals(headerKey, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers[headerKey] = headerValues;
                }
            }

            if (!HttpMethods.IsHead(method))
            {
                await context.Response.Body.WriteAsync(entry.Body, context.RequestAborted);
            }

            return;
        }

        // 2. Cache MISS: Capture upstream response
        context.Response.Headers[CacheHeaderName] = CacheMissHeaderValue;

        var originalBodyStream = context.Response.Body;
        await using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Position = 0;
            var responseBytes = memoryStream.ToArray();

            // Only cache successful 200 OK responses
            if (context.Response.StatusCode == StatusCodes.Status200OK && responseBytes.Length > 0)
            {
                var durationSeconds = 60;
                if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Cache.DurationSeconds, out var durStr) &&
                    int.TryParse(durStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDur) &&
                    parsedDur > 0)
                {
                    durationSeconds = parsedDur;
                }

                var capturedHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var (hKey, hVal) in context.Response.Headers)
                {
                    if (!string.Equals(hKey, CacheHeaderName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(hKey, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    {
                        capturedHeaders[hKey] = hVal.ToArray()!;
                    }
                }

                var cachedEntry = new CachedResponseEntry(
                    context.Response.StatusCode,
                    context.Response.ContentType,
                    responseBytes,
                    capturedHeaders);

                _memoryCache.Set(cacheKey, cachedEntry, TimeSpan.FromSeconds(durationSeconds));
            }

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static string BuildCacheKey(HttpContext context, IReadOnlyDictionary<string, string> metadata)
    {
        var sb = new StringBuilder();
        sb.Append(context.Request.Path.Value);

        // Query string filtering
        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Cache.VaryByQueryKeys, out var queryKeysStr) &&
            !string.IsNullOrWhiteSpace(queryKeysStr))
        {
            var queryKeys = queryKeysStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var qk in queryKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                if (context.Request.Query.TryGetValue(qk, out var qv))
                {
                    sb.Append("?").Append(qk).Append('=').Append(qv.ToString());
                }
            }
        }
        else if (context.Request.QueryString.HasValue)
        {
            sb.Append(context.Request.QueryString.Value);
        }

        // Vary by headers
        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Cache.VaryByHeaders, out var varyHeadersStr) &&
            !string.IsNullOrWhiteSpace(varyHeadersStr))
        {
            var varyHeaders = varyHeadersStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var vh in varyHeaders.OrderBy(h => h, StringComparer.OrdinalIgnoreCase))
            {
                if (context.Request.Headers.TryGetValue(vh, out var hv))
                {
                    sb.Append(";H:").Append(vh).Append('=').Append(hv.ToString());
                }
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Pipeline extension methods for registering <see cref="KyrolusEdgeCacheMiddleware"/>.
/// </summary>
public static class KyrolusEdgeCacheMiddlewareExtensions
{
    /// <summary>
    /// Adds edge response caching to the gateway HTTP request pipeline.
    /// </summary>
    /// <param name="app">The application pipeline builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseKyrolusEdgeCache(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<KyrolusEdgeCacheMiddleware>();
    }
}
