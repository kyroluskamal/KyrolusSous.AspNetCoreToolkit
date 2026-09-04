

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
                BusinessAction = businessAction,
                LocalizedAction = localizedAction,
                Category = auditable.AuditCategory,
                CommandName = requestType.Name,
                CommandFullName = requestType.FullName ?? requestType.Name,
                Payload = auditable.IncludePayload ? SanitizePayload(request) : null,
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
        if (_localizer is null || string.IsNullOrWhiteSpace(key))
            return null;

        var result = args is not null
            ? _localizer.GetString(key, args)
            : _localizer.GetString(key);

        return result.ResourceNotFound ? null : result.Value;
    }

    /// <summary>Bounds recursion into nested objects - deep enough for realistic DTO graphs, shallow enough that a self-referencing or pathological graph cannot recurse indefinitely.</summary>
    private const int MaxSanitizeDepth = 6;

    private const string RedactedPlaceholder = "***REDACTED***";
    private const string UnavailablePlaceholder = "***UNAVAILABLE***";

    private object? SanitizePayload(object? payload) => SanitizePayload(payload, depth: 0);

    private object? SanitizePayload(object? payload, int depth)
    {
        if (payload is null) return null;

        // A nested DTO's own sensitive properties (a PaymentDetails.CardNumber inside an order
        // command, say) must be redacted the same as a top-level one - only inspecting top-level
        // property names would pass nested sensitive data straight through to whatever the audit
        // sink logs, defeating the point of sanitizing at all.
        if (depth >= MaxSanitizeDepth || IsSimpleType(payload.GetType())) return payload;

        // A dictionary (e.g. the Updates bag on a Patch/BulkPatch/ExecuteUpdate command) is also
        // IEnumerable<KeyValuePair<,>>, so this check must run before the general IEnumerable branch
        // below - otherwise each entry gets reflected as a KeyValuePair and IsSensitive is checked
        // against the literal names "Key"/"Value" instead of the entry's actual key (e.g. "Password"),
        // and the real key never gets redacted at all.
        if (payload is System.Collections.IDictionary dictionary)
            return SanitizeDictionary(dictionary, depth);

        if (payload is System.Collections.IEnumerable enumerable and not string)
            return SanitizeEnumerable(enumerable, depth);

        return SanitizeObject(payload, depth);
    }

    private List<object?> SanitizeEnumerable(System.Collections.IEnumerable enumerable, int depth)
    {
        var items = new List<object?>();
        foreach (var item in enumerable)
        {
            // One pathological item (a lazy/computed value that throws on enumeration or on its own
            // sanitization) must not discard every other item already sanitized in this collection.
            try
            {
                items.Add(SanitizePayload(item, depth + 1));
            }
            catch
            {
                items.Add(UnavailablePlaceholder);
            }
        }
        return items;
    }

    private object SanitizeDictionary(System.Collections.IDictionary dictionary, int depth)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString() ?? "null";
            try
            {
                result[key] = IsSensitive(key)
                    ? RedactedPlaceholder
                    : SanitizePayload(entry.Value, depth + 1);
            }
            catch
            {
                result[key] = UnavailablePlaceholder;
            }
        }
        return result;
    }

    private object SanitizeObject(object payload, int depth)
    {
        var props = payload.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (props.Length == 0) return payload;

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in props)
        {
            if (prop.GetIndexParameters().Length > 0) continue; // skip indexers

            var name = prop.Name;
            if (IsSensitive(name))
            {
                dict[name] = RedactedPlaceholder;
                continue;
            }

            // A single property whose getter throws (a computed property touching a disposed
            // DbContext navigation, say) must not force the entire object to fall back to being
            // logged raw - that would re-expose every sensitive property already redacted above it.
            try
            {
                dict[name] = SanitizePayload(prop.GetValue(payload), depth + 1);
            }
            catch
            {
                dict[name] = UnavailablePlaceholder;
            }
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

    private static readonly string[] BuiltInSensitiveKeywords =
    [
        "password", "secret", "token", "pin", "cvv", "cardnumber", "apikey"
    ];

    private bool IsSensitive(string name)
    {
        foreach (var keyword in BuiltInSensitiveKeywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        }

        foreach (var keyword in _extraSensitiveKeywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) && name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
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
