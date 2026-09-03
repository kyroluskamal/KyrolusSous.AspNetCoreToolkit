using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Caching;

/// <summary>
/// The envelope stored under an idempotency key, so a "claimed but not finished yet" state can be
/// told apart from "finished, here is the response" without relying on equality-to-default - which
/// breaks for <see cref="KyrolusSous.Mediator.Abstractions.Interfaces.Unit"/>, whose every instance
/// compares equal to every other, including <see langword="default"/>.
/// </summary>
internal sealed class KyrolusIdempotencyRecord<TResponse>
{
    public bool Completed { get; set; }
    public TResponse? Response { get; set; }
}

/// <summary>
/// Pipeline behavior guaranteeing idempotency on commands implementing <see cref="IKyrolusIdempotentCommand"/> or <see cref="IKyrolusIdempotentCommand{TResponse}"/>.
/// </summary>
/// <remarks>
/// Claims the idempotency key atomically (via <see cref="IKyrolusCacheProvider.SetIfNotExistsAsync{T}"/>)
/// before running the handler, rather than checking for a cached result first and writing it after.
/// The check-then-write shape looks idempotent for sequential retries but is not: two requests that
/// arrive close enough together both see "not cached yet" and both run the handler, which is exactly
/// the double-execution idempotency exists to prevent. Claiming first closes that window - the second
/// concurrent caller sees the key already claimed and is told to retry rather than running the command
/// again or racing to overwrite the first caller's result.
/// </remarks>
[PipelineOrder(-800)]
public sealed class KyrolusIdempotencyBehavior<TRequest, TResponse>(
    IKyrolusCacheProvider? cacheProvider = null,
    ILogger<KyrolusIdempotencyBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusCacheProvider? _cacheProvider = cacheProvider;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        string? idempotencyKey = null;
        TimeSpan? idempotencyTtl = null;

        if (request is IKyrolusIdempotentCommand<TResponse> typedCmd)
        {
            idempotencyKey = typedCmd.IdempotencyKey;
            idempotencyTtl = typedCmd.IdempotencyTtl;
        }
        else if (request is IKyrolusIdempotentCommand nonGenericCmd)
        {
            idempotencyKey = nonGenericCmd.IdempotencyKey;
            idempotencyTtl = nonGenericCmd.IdempotencyTtl;
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || _cacheProvider is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = $"idempotency:{typeof(TRequest).Name}:{idempotencyKey}";
        var options = new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = idempotencyTtl ?? TimeSpan.FromHours(24)
        };

        var claimed = await _cacheProvider
            .SetIfNotExistsAsync(cacheKey, new KyrolusIdempotencyRecord<TResponse> { Completed = false }, options, cancellationToken)
            .ConfigureAwait(false);

        if (!claimed)
        {
            var existing = await _cacheProvider.GetAsync<KyrolusIdempotencyRecord<TResponse>>(cacheKey, cancellationToken).ConfigureAwait(false);
            if (existing is { Completed: true })
            {
                _logger?.LogInformation(
                    "[Kyrolus CQRS] Idempotent hit: Command {RequestType} with key '{IdempotencyKey}' was previously executed. Returning cached response.",
                    typeof(TRequest).Name,
                    idempotencyKey);
                return existing.Response!;
            }

            // Claimed by someone else and not finished yet - a concurrent duplicate, not a retry
            // after completion. Running the handler here too would be the exact double-execution
            // this behavior exists to prevent, so this is reported rather than silently retried.
            _logger?.LogWarning(
                "[Kyrolus CQRS] Idempotency conflict: {RequestType} with key '{IdempotencyKey}' is already in progress.",
                typeof(TRequest).Name,
                idempotencyKey);
            throw new KyrolusIdempotencyConflictException(idempotencyKey);
        }

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            await _cacheProvider
                .SetAsync(cacheKey, new KyrolusIdempotencyRecord<TResponse> { Completed = true, Response = response }, options, cancellationToken)
                .ConfigureAwait(false);
            return response;
        }
        catch
        {
            // The handler failed - release the claim so a genuine retry with the same key can proceed
            // instead of being stuck behind a claim that will never complete.
            await _cacheProvider.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
