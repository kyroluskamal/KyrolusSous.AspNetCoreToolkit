namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public sealed record KyrolusIdempotencyOptions(
    bool Enabled,
    bool IncludeGet,
    string HeaderName,
    TimeSpan Ttl);

public static class KyrolusEndpointPolicies
{
    public static RouteHandlerBuilder ApplyEndpointPolicies<TResponse>(
        this RouteHandlerBuilder builder,
        IKyrolusApiConfig<TResponse> config,
        EndpointNames endpoint)
        where TResponse : class
    {
        var endpointConfig = config.EndpointConfig?.FirstOrDefault(e => e.Name == endpoint);
        var rateLimitPolicy = endpointConfig?.RateLimitPolicy ?? config.RateLimitPolicy;
        if (!string.IsNullOrWhiteSpace(rateLimitPolicy))
        {
            builder.RequireRateLimiting(rateLimitPolicy);
        }

        var enableIdempotency = endpointConfig?.Idempotent ?? config.EnableIdempotency;
        if (enableIdempotency)
        {
            var headerName = string.IsNullOrWhiteSpace(config.IdempotencyHeaderName)
                ? "Idempotency-Key"
                : config.IdempotencyHeaderName;
            var options = new KyrolusIdempotencyOptions(
                true,
                config.IdempotencyIncludeGet,
                headerName,
                config.IdempotencyTtl ?? TimeSpan.FromMinutes(10));

            builder.AddEndpointFilterFactory((context, next) =>
            {
                var store = context.ApplicationServices.GetRequiredService<IKyrolusIdempotencyStore>();
                var filter = new KyrolusIdempotencyEndpointFilter(store, options);
                return invocation => filter.InvokeAsync(invocation, next);
            });
        }

        builder.AddEndpointFilterFactory((context, next) =>
        {
            var filter = new KyrolusOutputCacheEndpointFilter<TResponse>(config, endpoint);
            return invocation => filter.InvokeAsync(invocation, next);
        });

        return builder;
    }
}

internal sealed class KyrolusIdempotencyEndpointFilter : IEndpointFilter
{
    private readonly IKyrolusIdempotencyStore store;
    private readonly KyrolusIdempotencyOptions options;

    public KyrolusIdempotencyEndpointFilter(IKyrolusIdempotencyStore store, KyrolusIdempotencyOptions options)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!options.Enabled)
        {
            return await next(context).ConfigureAwait(false);
        }

        var request = context.HttpContext.Request;
        if (!options.IncludeGet && HttpMethods.IsGet(request.Method))
        {
            return await next(context).ConfigureAwait(false);
        }

        if (!request.Headers.TryGetValue(options.HeaderName, out var headerValues))
        {
            return await next(context).ConfigureAwait(false);
        }

        var headerValue = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return await next(context).ConfigureAwait(false);
        }

        var cacheKey = $"{request.Method}:{request.Path}{request.QueryString}:{headerValue}";
        var cached = await store.GetAsync(cacheKey, context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (cached is not null)
        {
            return Results.Json(
                cached.Value,
                statusCode: cached.StatusCode,
                contentType: string.IsNullOrWhiteSpace(cached.ContentType) ? "application/json" : cached.ContentType);
        }

        var result = await next(context).ConfigureAwait(false);
        if (result is IValueHttpResult valueResult)
        {
            var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
            if (statusCode is >= 200 and < 300)
            {
                var contentType = (result as IContentTypeHttpResult)?.ContentType;
                var entry = new KyrolusIdempotencyEntry(valueResult.Value, statusCode, contentType);
                await store.SetAsync(cacheKey, entry, options.Ttl, context.HttpContext.RequestAborted).ConfigureAwait(false);
            }
        }

        return result;
    }
}
