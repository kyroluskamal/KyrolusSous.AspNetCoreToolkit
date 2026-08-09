using KyrolusSous.DataProtection.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusKeyRingRefreshNotifierListener(
    IKyrolusKeyRingRefreshNotifier notifier,
    KyrolusKeyRingRefreshTokenSource tokenSource,
    IOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions> refreshOptions,
    IOptions<KyrolusDataProtectionOptions> dataProtectionOptions,
    KyrolusDataProtectionInstanceId instanceId,
    ILogger<KyrolusKeyRingRefreshNotifierListener> logger)
    : BackgroundService
{
    private readonly IKyrolusKeyRingRefreshNotifier notifier =
        notifier ?? throw new ArgumentNullException(nameof(notifier));
    private readonly KyrolusKeyRingRefreshTokenSource tokenSource =
        tokenSource ?? throw new ArgumentNullException(nameof(tokenSource));
    private readonly IOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions> refreshOptions =
        refreshOptions ?? throw new ArgumentNullException(nameof(refreshOptions));
    private readonly KyrolusDataProtectionOptions dataProtectionOptions =
        dataProtectionOptions?.Value ?? throw new ArgumentNullException(nameof(dataProtectionOptions));
    private readonly KyrolusDataProtectionInstanceId instanceId =
        instanceId ?? throw new ArgumentNullException(nameof(instanceId));
    private readonly ILogger<KyrolusKeyRingRefreshNotifierListener> logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return notifier.ListenAsync(HandleSignalAsync, stoppingToken);
    }

    private Task HandleSignalAsync(KyrolusKeyRingRefreshSignal signal, CancellationToken cancellationToken)
    {
        var current = refreshOptions.CurrentValue;
        if (!current.EnableCrossInstanceNotifications || !current.RefreshOnExternalSignal)
        {
            return Task.CompletedTask;
        }

        if (!string.Equals(signal.ApplicationName, dataProtectionOptions.ApplicationName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (string.Equals(signal.InstanceId, instanceId.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        tokenSource.SignalExternal();
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Key ring refresh signal received from {InstanceId} ({Reason}).",
                signal.InstanceId,
                signal.Reason);
        return Task.CompletedTask;
    }
}
