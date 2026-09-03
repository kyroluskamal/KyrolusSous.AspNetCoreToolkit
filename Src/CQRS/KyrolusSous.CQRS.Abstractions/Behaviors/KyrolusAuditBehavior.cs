

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
    ILogger<KyrolusAuditBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusAuditSink? _auditSink = auditSink;
    private readonly IKyrolusCurrentUserContext? _userContext = userContext;
    private readonly ILogger? _logger = logger;

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
                Category = auditable.AuditCategory,
                RequestType = requestType.FullName ?? requestType.Name,
                RequestName = requestType.Name,
                Payload = auditable.IncludePayload ? SanitizePayload(request) : null,
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
                Category = auditable.AuditCategory,
                RequestType = requestType.FullName ?? requestType.Name,
                RequestName = requestType.Name,
                Payload = auditable.IncludePayload ? SanitizePayload(request) : null,
                DurationMs = sw.ElapsedMilliseconds,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };

            await EmitQuietlyAsync(failedEntry, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Bounds recursion into nested objects - deep enough for realistic DTO graphs, shallow enough that a self-referencing or pathological graph cannot recurse indefinitely.</summary>
    private const int MaxSanitizeDepth = 6;

    private static object? SanitizePayload(object? payload) => SanitizePayload(payload, depth: 0);

    private static object? SanitizePayload(object? payload, int depth)
    {
        if (payload is null) return null;

        // A nested DTO's own sensitive properties (a PaymentDetails.CardNumber inside an order
        // command, say) must be redacted the same as a top-level one - only inspecting top-level
        // property names would pass nested sensitive data straight through to whatever the audit
        // sink logs, defeating the point of sanitizing at all.
        if (depth >= MaxSanitizeDepth || IsSimpleType(payload.GetType())) return payload;

        try
        {
            if (payload is System.Collections.IEnumerable enumerable and not string)
                return SanitizeEnumerable(enumerable, depth);

            return SanitizeObject(payload, depth);
        }
        catch
        {
            return payload;
        }
    }

    private static List<object?> SanitizeEnumerable(System.Collections.IEnumerable enumerable, int depth)
    {
        var items = new List<object?>();
        foreach (var item in enumerable)
            items.Add(SanitizePayload(item, depth + 1));
        return items;
    }

    private static object SanitizeObject(object payload, int depth)
    {
        var props = payload.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (props.Length == 0) return payload;

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in props)
        {
            if (prop.GetIndexParameters().Length > 0) continue; // skip indexers

            var name = prop.Name;
            dict[name] = IsSensitive(name)
                ? "***REDACTED***"
                : SanitizePayload(prop.GetValue(payload), depth + 1);
        }
        return dict;
    }

    private static bool IsSimpleType(Type type)
        => type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(TimeSpan)
        || type == typeof(Guid)
        || type == typeof(Uri)
        || (Nullable.GetUnderlyingType(type) is { } underlying && IsSimpleType(underlying));

    private static bool IsSensitive(string name)
    {
        return name.Contains("password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("token", StringComparison.OrdinalIgnoreCase)
            || name.Contains("pin", StringComparison.OrdinalIgnoreCase)
            || name.Contains("cvv", StringComparison.OrdinalIgnoreCase)
            || name.Contains("cardnumber", StringComparison.OrdinalIgnoreCase);
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
