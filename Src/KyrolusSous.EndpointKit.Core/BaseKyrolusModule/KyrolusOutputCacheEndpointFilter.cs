using KyrolusSous.Caching.Abstractions;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

internal sealed class KyrolusOutputCacheEndpointFilter<TResponse>(
    IKyrolusApiConfig<TResponse> config,
    EndpointNames endpoint)
    : IEndpointFilter
    where TResponse : class
{
    private readonly IKyrolusApiConfig<TResponse> config = config;
    private readonly EndpointNames endpoint = endpoint;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsGet(request.Method))
        {
            return await next(context).ConfigureAwait(false);
        }

        var cache = context.HttpContext.RequestServices.GetService<ICacheProvider>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<KyrolusOutputCacheEndpointFilter<TResponse>>>();
        if (cache is null || cache is NullCacheProvider)
        {
            return await next(context).ConfigureAwait(false);
        }

        var policy = await ResolvePolicyAsync(context).ConfigureAwait(false);
        ApplyCacheControl(context.HttpContext.Response, policy);
        if (!IsCacheEnabled(policy))
        {
            return await next(context).ConfigureAwait(false);
        }

        var cacheKey = BuildCacheKey(context, policy.KeySuffix);
        var cached = await cache.GetAsync<KyrolusOutputCacheEntry>(cacheKey, context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (cached is not null)
        {
            logger?.LogInformation("Output cache hit {Path}", request.Path);
            return Results.Json(
                cached.Value,
                statusCode: cached.StatusCode,
                contentType: string.IsNullOrWhiteSpace(cached.ContentType) ? "application/json" : cached.ContentType);
        }

        logger?.LogInformation("Output cache miss {Path}", request.Path);
        var result = await next(context).ConfigureAwait(false);
        if (result is IValueHttpResult valueResult)
        {
            var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
            if (statusCode is >= 200 and < 300)
            {
                var entry = new KyrolusOutputCacheEntry(
                    valueResult.Value,
                    statusCode,
                    (result as IContentTypeHttpResult)?.ContentType);
                var options = BuildEntryOptions(context, policy);
                await cache.SetAsync(cacheKey, entry, options, context.HttpContext.RequestAborted).ConfigureAwait(false);
            }
        }

        return result;
    }

    private async ValueTask<KyrolusCachePolicy> ResolvePolicyAsync(EndpointFilterInvocationContext context)
    {
        var endpointConfig = config.EndpointConfig?.FirstOrDefault(e => e.Name == endpoint);
        var enabled = endpointConfig?.OutputCacheEnabled ?? config.EnableOutputCaching;
        var effective = new KyrolusCachePolicy(Enabled: enabled);

        effective = MergePolicy(effective, config.OutputCachePolicy);
        effective = MergePolicy(effective, ResolveAttributePolicy(endpointConfig));
        effective = MergePolicy(effective, endpointConfig?.OutputCachePolicy);

        var provider = context.HttpContext.RequestServices.GetService<IKyrolusEndpointCachePolicyProvider>();
        if (provider is not null)
        {
            var keyContext = context.HttpContext.RequestServices.GetService<ICacheKeyContext>();
            var policyContext = new KyrolusEndpointCachePolicyContext(
                typeof(TResponse),
                typeof(TResponse).Name,
                endpoint,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path,
                keyContext?.TenantId,
                keyContext?.ScopeKey);
            var dynamicPolicy = await provider.GetPolicyAsync(policyContext, context.HttpContext.RequestAborted).ConfigureAwait(false);
            effective = MergePolicy(effective, dynamicPolicy);
        }

        return effective;
    }

    private KyrolusCachePolicy? ResolveAttributePolicy(IEndpointConfig? endpointConfig)
    {
        var viewModelType = endpointConfig?.ViewModelType ?? config.ViewModelType ?? typeof(TResponse);
        var attribute = viewModelType.GetCustomAttribute<KyrolusOutputCacheAttribute>();
        return attribute?.ToPolicy();
    }

    private static bool IsCacheEnabled(KyrolusCachePolicy policy)
        => policy.Enabled.GetValueOrDefault();

    private static KyrolusCachePolicy MergePolicy(KyrolusCachePolicy basePolicy, KyrolusCachePolicy? overridePolicy)
    {
        if (overridePolicy is null) return basePolicy;
        return new KyrolusCachePolicy(
            AbsoluteExpirationRelativeToNow: overridePolicy.AbsoluteExpirationRelativeToNow ?? basePolicy.AbsoluteExpirationRelativeToNow,
            SlidingExpiration: overridePolicy.SlidingExpiration ?? basePolicy.SlidingExpiration,
            Jitter: overridePolicy.Jitter ?? basePolicy.Jitter,
            NegativeCacheTtl: overridePolicy.NegativeCacheTtl ?? basePolicy.NegativeCacheTtl,
            Enabled: overridePolicy.Enabled ?? basePolicy.Enabled,
            KeySuffix: overridePolicy.KeySuffix ?? basePolicy.KeySuffix,
            ExtraInvalidationKeys: basePolicy.ExtraInvalidationKeys,
            ExtraInvalidationKeyPatterns: basePolicy.ExtraInvalidationKeyPatterns);
    }

    private static KyrolusCacheEntryOptions BuildEntryOptions(EndpointFilterInvocationContext context, KyrolusCachePolicy policy)
    {
        var keyContext = context.HttpContext.RequestServices.GetService<ICacheKeyContext>();
        return new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = policy.AbsoluteExpirationRelativeToNow,
            SlidingExpiration = policy.SlidingExpiration,
            Jitter = policy.Jitter,
            NegativeExpirationRelativeToNow = policy.NegativeCacheTtl,
            Region = keyContext?.Region,
            TenantId = keyContext?.TenantId
        };
    }

    private static void ApplyCacheControl(HttpResponse response, KyrolusCachePolicy policy)
    {
        var ttl = policy.AbsoluteExpirationRelativeToNow ?? policy.SlidingExpiration;
        if (!IsCacheEnabled(policy) || ttl is null || ttl.Value <= TimeSpan.Zero)
        {
            response.Headers.CacheControl = "no-store";
            return;
        }

        var seconds = Math.Max(0, (int)ttl.Value.TotalSeconds);
        response.Headers.CacheControl = $"private,max-age={seconds}";
    }

    private string BuildCacheKey(EndpointFilterInvocationContext context, string? suffix)
    {
        var request = context.HttpContext.Request;
        var keyContext = context.HttpContext.RequestServices.GetService<ICacheKeyContext>();
        var accept = request.Headers.Accept.ToString();
        var key = $"out:{request.Method}:{request.Path}{request.QueryString}";
        if (!string.IsNullOrWhiteSpace(accept)) key += $":accept={Uri.EscapeDataString(accept)}";
        if (!string.IsNullOrWhiteSpace(keyContext?.ScopeKey)) key += $":scope={Uri.EscapeDataString(keyContext.ScopeKey)}";
        if (!string.IsNullOrWhiteSpace(keyContext?.TenantId)) key += $":tenant={Uri.EscapeDataString(keyContext.TenantId)}";
        if (!string.IsNullOrWhiteSpace(suffix)) key += $":policy={Uri.EscapeDataString(suffix)}";
        return key;
    }
}
