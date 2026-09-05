using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.LivePush;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior broadcasting real-time notifications upon successful execution of <see cref="IKyrolusLivePushCommand"/>.
/// </summary>
/// <remarks>
/// Ordered outer (more negative) than <c>KyrolusReadModelProjectionBehavior</c> (-600),
/// <c>KyrolusCommandCacheInvalidationBehavior</c> (-560, in <c>KyrolusSous.CQRS.Caching</c>) and the
/// EF/Marten <c>DomainEventsDispatchBehavior</c> (-650), so the broadcast is the LAST post-write side
/// effect to run, after all of those have already updated their own state. Broadcasting first (its
/// previous position, -550, inside all three) meant a subscriber that reacts to the push by
/// immediately re-querying could still see a stale cached response or a stale read model - the
/// notification arrived before the things it was announcing were actually done.
/// <para>
/// The broadcast payload is redacted through <see cref="KyrolusSensitiveDataRedactor"/> before it
/// reaches <see cref="IKyrolusLivePushPublisher.PublishLiveAsync"/> - the same logic
/// <see cref="KyrolusAuditBehavior{TRequest,TResponse}"/> uses for its sink. A live-push destination
/// (every subscriber connected to <see cref="IKyrolusLivePushCommand.Channel"/>) is architecturally
/// MORE exposed than an audit sink, not less, so broadcasting <c>PushData</c>/response/request
/// completely unredacted - as this behavior used to - was a bigger leak surface than the equivalent gap
/// would have been in audit logging. This intentionally reuses <see cref="KyrolusAuditSanitizationOptions"/>
/// rather than introducing a second, LivePush-specific options type: one app-wide sensitive-keyword
/// list is simpler to reason about than two lists that can silently diverge, and an application that
/// already configured extra keywords for auditing (via <c>AddKyrolusCqrsAudit</c>'s
/// <c>configureSanitization</c>) gets the same protection here automatically, with no separate
/// registration call. If <c>AddKyrolusCqrsAudit</c> was never called, <paramref name="sanitizationOptions"/>
/// simply resolves to <see langword="null"/> and this behavior falls back to the built-in keyword list,
/// same as <c>KyrolusAuditBehavior</c> does in that case.
/// </para>
/// </remarks>
[PipelineOrder(-660)]
public sealed class KyrolusLivePushBehavior<TRequest, TResponse>(
    IKyrolusLivePushPublisher? publisher = null,
    ILogger<KyrolusLivePushBehavior<TRequest, TResponse>>? logger = null,
    KyrolusAuditSanitizationOptions? sanitizationOptions = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusLivePushPublisher? _publisher = publisher;
    private readonly ILogger? _logger = logger;
    private readonly string[] _extraSensitiveKeywords = sanitizationOptions?.AdditionalSensitiveKeywords is { Count: > 0 } extra
        ? [.. extra]
        : [];

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(cancellationToken).ConfigureAwait(false);

        if (request is IKyrolusLivePushCommand liveCommand && _publisher is not null)
        {
            try
            {
                var payload = liveCommand.PushData ?? response ?? (object)request;
                var sanitizedPayload = KyrolusSensitiveDataRedactor.Sanitize(payload, _extraSensitiveKeywords);
                await _publisher.PublishLiveAsync(liveCommand.Channel, sanitizedPayload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[Kyrolus CQRS LivePush] Failed to broadcast live notification on channel '{Channel}'", liveCommand.Channel);
            }
        }

        return response;
    }
}
