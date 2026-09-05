using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
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
    ILogger<KyrolusIdempotencyBehavior<TRequest, TResponse>>? logger = null,
    IKyrolusCurrentUserContext? userContext = null,
    TimeSpan? claimRenewalInterval = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusCacheProvider? _cacheProvider = cacheProvider;
    private readonly ILogger? _logger = logger;
    private readonly IKyrolusCurrentUserContext? _userContext = userContext;

    /// <summary>
    /// Interval between periodic claim-TTL renewals while the handler executes. Defaults to
    /// <see cref="KyrolusIdempotencyLimits.DefaultClaimRenewalInterval"/>; overridable (e.g. by tests
    /// that need the renewal loop to tick within milliseconds rather than minutes) without touching the
    /// process-wide default.
    /// </summary>
    private readonly TimeSpan _claimRenewalInterval = claimRenewalInterval ?? KyrolusIdempotencyLimits.DefaultClaimRenewalInterval;

    private static int _nullCacheProviderWarned;

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

        WarnOnceIfNullCacheProvider();

        var cacheKey = BuildCacheKey(idempotencyKey);
        var claimOptions = new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = KyrolusIdempotencyLimits.InProgressClaimTtl
        };
        var completedOptions = new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = idempotencyTtl ?? TimeSpan.FromHours(24)
        };

        var claimed = await _cacheProvider
            .SetIfNotExistsAsync(cacheKey, new KyrolusIdempotencyRecord<TResponse> { Completed = false }, claimOptions, cancellationToken)
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

        // Renews the claim's short TTL for as long as `next()` is genuinely still running, so a
        // legitimately slow handler never has its claim expire mid-flight (see
        // KyrolusIdempotencyLimits.InProgressClaimTtl's remarks). This CTS is deliberately its own,
        // NOT linked to `cancellationToken`: the goal is "keep renewing until next() actually returns
        // or throws", regardless of whether the caller's token gets cancelled first - a handler that
        // keeps doing real work past cancellation (or hasn't observed it yet) must still be protected.
        // The try/finally below guarantees the loop is always cancelled and awaited to completion
        // before this method returns, so no renewal timer is ever left running past Handle's lifetime.
        using var renewalCts = new CancellationTokenSource();
        var renewalTask = RenewClaimPeriodicallyAsync(_cacheProvider, cacheKey, claimOptions, renewalCts.Token);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            await _cacheProvider
                .SetAsync(cacheKey, new KyrolusIdempotencyRecord<TResponse> { Completed = true, Response = response }, completedOptions, cancellationToken)
                .ConfigureAwait(false);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The handler may already have finished its real work by the time cancellation surfaced
            // (the caller gave up waiting for the response, not before it happened) - releasing the
            // claim here would let a retry with the same key run it again.
            throw;
        }
        catch
        {
            // The handler failed - release the claim so a genuine retry with the same key can proceed
            // instead of being stuck behind a claim that will never complete.
            await _cacheProvider.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            // Stop renewing the instant next() has returned or thrown (both handled above) - not a
            // moment later. Awaiting the task here (rather than fire-and-forget cancelling it) is what
            // guarantees the loop has actually exited before Handle's own Task completes.
            renewalCts.Cancel();
            try
            {
                await renewalTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: this is the loop observing the cancellation requested above and exiting.
            }
        }
    }

    /// <summary>
    /// Periodically re-writes the in-progress claim record with a fresh <see cref="KyrolusIdempotencyLimits.InProgressClaimTtl"/>
    /// window, so the claim never lapses while the handler it is guarding is still actually running.
    /// </summary>
    /// <remarks>
    /// Takes <paramref name="cacheProvider"/> as a parameter (rather than reading <c>_cacheProvider</c>
    /// directly) purely so the compiler's nullable-flow narrowing at the call site - already proven
    /// non-null there - carries through without a null-forgiving operator; this method has no other
    /// reason to differ from an instance method.
    /// </remarks>
    private async Task RenewClaimPeriodicallyAsync(
        IKyrolusCacheProvider cacheProvider,
        string cacheKey,
        KyrolusCacheEntryOptions claimOptions,
        CancellationToken renewalToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_claimRenewalInterval, renewalToken).ConfigureAwait(false);

                await cacheProvider
                    .SetAsync(cacheKey, new KyrolusIdempotencyRecord<TResponse> { Completed = false }, claimOptions, renewalToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop condition: the handler finished (successfully or not) before the next
            // renewal tick, and Handle's finally block cancelled renewalToken - this is not an error.
        }
    }

    private void WarnOnceIfNullCacheProvider()
    {
        // KyrolusNullCacheProvider is a fully no-op IKyrolusCacheProvider (see its own docs), so it
        // passes the "is null" guard above but silently makes every claim/lookup here a no-op:
        // SetIfNotExistsAsync always reports success and nothing is ever actually persisted, so
        // idempotency provides no real protection at all - a misconfiguration worth surfacing loudly,
        // once, rather than letting it fail silently forever. Guarded with Interlocked (not a plain
        // bool) because this behavior is registered Scoped - a new instance per request - so a plain
        // instance field would re-log on every single request instead of once per closed generic type.
        if (_cacheProvider is KyrolusNullCacheProvider
            && Interlocked.CompareExchange(ref _nullCacheProviderWarned, 1, 0) == 0)
        {
            _logger?.LogWarning(
                "[Kyrolus CQRS] {Behavior} for {RequestType} is backed by KyrolusNullCacheProvider - " +
                "idempotency claims and completed-result lookups are no-ops, so duplicate requests will " +
                "re-execute the handler instead of being deduplicated. Register a real IKyrolusCacheProvider " +
                "if this was not intentional.",
                nameof(KyrolusIdempotencyBehavior<TRequest, TResponse>),
                typeof(TRequest).Name);
        }
    }

    /// <summary>
    /// Builds the idempotency cache key, always prefixed with the current tenant/user.
    /// </summary>
    /// <remarks>
    /// See <see cref="KyrolusQueryCachingBehavior{TRequest,TResponse}.ScopeKey"/> for the same
    /// reasoning applied to query caching: a flat <see cref="IKyrolusCacheProvider"/> has no built-in
    /// isolation, so anything that does not fold tenant/user into the key itself is effectively
    /// shared across every caller. The idempotency case is worse than query caching - a hit here
    /// hands back a previously-computed <em>command response</em> verbatim (see <c>existing.Response</c>
    /// above), not just a cached read - and client-supplied idempotency keys are commonly derived from
    /// business identifiers (<c>"invoice-{orderNumber}"</c>) that can collide across tenants whose own
    /// numbering resets independently. Unlike <c>ScopeKey</c>, this prefixing is unconditional: there
    /// is no <see cref="KyrolusSous.CQRS.Abstractions.Interfaces.IKyrolusCacheableRequest.IsSharedAcrossUsers"/>-style
    /// opt-out, because a claimed-and-executed command result always belongs to whoever submitted it,
    /// and it applies even when no <see cref="IKyrolusCurrentUserContext"/> is registered at all (a
    /// missing tenant/user becomes <c>"-"</c>, the same placeholder <c>ScopeKey</c> uses, rather than
    /// skipping the prefix the way <c>ScopeKey</c> does for an unregistered context).
    /// </remarks>
    private string BuildCacheKey(string idempotencyKey)
    {
        var tenant = string.IsNullOrWhiteSpace(_userContext?.TenantId) ? "-" : _userContext.TenantId;
        var user = string.IsNullOrWhiteSpace(_userContext?.UserId) ? "-" : _userContext.UserId;
        return $"tenant:{tenant}:user:{user}:idempotency:{typeof(TRequest).FullName ?? typeof(TRequest).Name}:{idempotencyKey}";
    }
}
