namespace KyrolusSous.CQRS.Abstractions.Audit;

/// <summary>
/// Default audit sink that writes structured audit log entries via Microsoft.Extensions.Logging.
/// </summary>
public sealed class LoggerAuditSink(ILogger<LoggerAuditSink> logger) : IKyrolusAuditSink
{
    private readonly ILogger _logger = logger;

    public Task EmitAsync(KyrolusAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsSuccess)
        {
            _logger.LogInformation(
                "[Kyrolus CQRS Audit] Action '{Action}' ({CommandName}) executed by User '{UserId}' (Tenant: '{TenantId}') in {DurationMs}ms - Success",
                entry.Action,
                entry.CommandName,
                entry.UserId ?? "Anonymous",
                entry.TenantId ?? "None",
                entry.DurationMs);
        }
        else
        {
            _logger.LogWarning(
                "[Kyrolus CQRS Audit] Action '{Action}' ({CommandName}) executed by User '{UserId}' (Tenant: '{TenantId}') in {DurationMs}ms - Failed: {ErrorMessage}",
                entry.Action,
                entry.CommandName,
                entry.UserId ?? "Anonymous",
                entry.TenantId ?? "None",
                entry.DurationMs,
                entry.ErrorMessage);
        }

        return Task.CompletedTask;
    }
}
