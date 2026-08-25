using System.Diagnostics;
using KyrolusSous.CQRS.Abstractions.Audit;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior capturing and emitting audit trail records for auditable CQRS commands.
/// </summary>
[PipelineOrder(-850)]
public sealed class KyrolusAuditBehavior<TRequest, TResponse>(
    IAuditSink? auditSink = null,
    ICurrentUserContext? userContext = null,
    ILogger<KyrolusAuditBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IAuditSink? _auditSink = auditSink;
    private readonly ICurrentUserContext? _userContext = userContext;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IAuditableCommand auditable || _auditSink is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var sw = Stopwatch.StartNew();
        var requestType = typeof(TRequest);
        var actionName = !string.IsNullOrWhiteSpace(auditable.AuditAction) ? auditable.AuditAction : requestType.Name;
        var context = _userContext ?? new DefaultCurrentUserContext();

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

    private static object? SanitizePayload(object? payload)
    {
        if (payload is null) return null;
        try
        {
            var props = payload.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (props.Length == 0) return payload;

            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in props)
            {
                var name = prop.Name;
                if (IsSensitive(name))
                {
                    dict[name] = "***REDACTED***";
                }
                else
                {
                    dict[name] = prop.GetValue(payload);
                }
            }
            return dict;
        }
        catch
        {
            return payload;
        }
    }

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
            {
                await _auditSink.EmitAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Sinks failing must never crash the primary business transaction
            _logger?.LogWarning(ex, "[Kyrolus CQRS Audit] Failed to emit audit entry for action '{Action}'", entry.Action);
        }
    }
}
