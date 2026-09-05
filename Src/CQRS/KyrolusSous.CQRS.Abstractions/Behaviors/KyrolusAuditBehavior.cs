

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior capturing and emitting audit trail records for auditable CQRS commands.
/// </summary>
/// <remarks>
/// Ordered as the second-outermost behavior - just inside <c>KyrolusExceptionMappingBehavior</c>
/// (-2100) and outside everything else, including <c>KyrolusAuthorizationBehavior</c> (-1050) and
/// <c>KyrolusTenantScopingBehavior</c> (-1040). At its previous position (-850, inside both), a
/// denied request never reached this behavior's <c>Handle</c> at all: <see cref="KyrolusSecurityException"/>
/// was thrown by Authorization/TenantScoping before the call chain ever got here, so a rejected
/// access attempt on an auditable command left no audit trail - exactly the kind of event an audit
/// log exists to capture. Moving here means this now also wraps <c>KyrolusValidationBehavior</c> and
/// every other inner behavior, so a request that fails validation (or anything else downstream) on an
/// auditable command is recorded too - a deliberate widening, not a side effect: the previous
/// exclusion only made sense while this ran inside Authorization/TenantScoping in the first place.
/// </remarks>
[PipelineOrder(-2050)]
public sealed class KyrolusAuditBehavior<TRequest, TResponse>(
    IKyrolusAuditSink? auditSink = null,
    IKyrolusCurrentUserContext? userContext = null,
    IKyrolusLocalizer? localizer = null,
    ILogger<KyrolusAuditBehavior<TRequest, TResponse>>? logger = null,
    KyrolusAuditSanitizationOptions? sanitizationOptions = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusAuditSink? _auditSink = auditSink;
    private readonly IKyrolusCurrentUserContext? _userContext = userContext;
    private readonly IKyrolusLocalizer? _localizer = localizer;
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

        if (request is not IKyrolusAuditableCommand auditable || _auditSink is null)
            return await next(cancellationToken).ConfigureAwait(false);


        var sw = Stopwatch.StartNew();
        var requestType = typeof(TRequest);
        var actionName = !string.IsNullOrWhiteSpace(auditable.AuditAction) ? auditable.AuditAction : requestType.Name;
        var businessAction = !string.IsNullOrWhiteSpace(auditable.BusinessAction) ? auditable.BusinessAction : auditable.AuditAction;
        var localizedAction = ResolveLocalizedAction(businessAction ?? actionName, auditable.BusinessActionArgs);
        var context = _userContext ?? new KyrolusDefaultCurrentUserContext();

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            var entry = new KyrolusAuditEntry
            {
                UserId = context.UserId,
                UserName = context.UserName,
                TenantId = context.TenantId,
                Action = actionName,
                BusinessAction = businessAction,
                LocalizedAction = localizedAction,
                Category = auditable.AuditCategory,
                CommandName = requestType.Name,
                CommandFullName = requestType.FullName ?? requestType.Name,
                Payload = auditable.IncludePayload ? KyrolusSensitiveDataRedactor.Sanitize(request, _extraSensitiveKeywords) : null,
                DurationMs = sw.ElapsedMilliseconds,
                IsSuccess = true
            };

            await EmitQuietlyAsync(entry, cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();

            var failedEntry = new KyrolusAuditEntry
            {
                UserId = context.UserId,
                UserName = context.UserName,
                TenantId = context.TenantId,
                Action = actionName,
                BusinessAction = businessAction,
                LocalizedAction = localizedAction,
                Category = auditable.AuditCategory,
                CommandName = requestType.Name,
                CommandFullName = requestType.FullName ?? requestType.Name,
                Payload = auditable.IncludePayload ? KyrolusSensitiveDataRedactor.Sanitize(request, _extraSensitiveKeywords) : null,
                DurationMs = sw.ElapsedMilliseconds,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };

            await EmitQuietlyAsync(failedEntry, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private string? ResolveLocalizedAction(string? key, object? args)
    {
        if (_localizer is null || string.IsNullOrWhiteSpace(key)) return null;

        var result = args is not null
            ? _localizer.GetString(key, args)
            : _localizer.GetString(key);

        return result.ResourceNotFound ? null : result.Value;
    }

    private async Task EmitQuietlyAsync(KyrolusAuditEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditSink is not null)
                await _auditSink.EmitAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Sinks failing must never crash the primary business transaction
            _logger?.LogWarning(ex, "[Kyrolus CQRS Audit] Failed to emit audit entry for action '{Action}'", entry.Action);
        }
    }
}
