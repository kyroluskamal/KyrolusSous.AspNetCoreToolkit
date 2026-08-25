using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Caching;

/// <summary>
/// Pipeline behavior guaranteeing idempotency on commands implementing <see cref="IIdempotentCommand"/> or <see cref="IIdempotentCommand{TResponse}"/>.
/// </summary>
[PipelineOrder(-800)]
public sealed class KyrolusIdempotencyBehavior<TRequest, TResponse>(
    ICacheProvider? cacheProvider = null,
    ILogger<KyrolusIdempotencyBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly ICacheProvider? _cacheProvider = cacheProvider;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        string? idempotencyKey = null;
        TimeSpan? idempotencyTtl = null;

        if (request is IIdempotentCommand<TResponse> typedCmd)
        {
            idempotencyKey = typedCmd.IdempotencyKey;
            idempotencyTtl = typedCmd.IdempotencyTtl;
        }
        else if (request is IIdempotentCommand nonGenericCmd)
        {
            idempotencyKey = nonGenericCmd.IdempotencyKey;
            idempotencyTtl = nonGenericCmd.IdempotencyTtl;
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || _cacheProvider is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = $"idempotency:{typeof(TRequest).Name}:{idempotencyKey}";

        var cached = await _cacheProvider.GetAsync<TResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null && !EqualityComparer<TResponse>.Default.Equals(cached, default))
        {
            _logger?.LogInformation(
                "[Kyrolus CQRS] Idempotent hit: Command {RequestType} with key '{IdempotencyKey}' was previously executed. Returning cached response.",
                typeof(TRequest).Name,
                idempotencyKey);
            return cached;
        }

        var response = await next(cancellationToken).ConfigureAwait(false);

        var options = new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = idempotencyTtl ?? TimeSpan.FromHours(24)
        };

        await _cacheProvider.SetAsync(cacheKey, response, options, cancellationToken).ConfigureAwait(false);
        return response;
    }
}
