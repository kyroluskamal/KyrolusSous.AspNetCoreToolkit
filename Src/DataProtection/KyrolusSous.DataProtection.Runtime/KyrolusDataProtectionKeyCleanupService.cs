using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionKeyCleanupService(
    IKeyManager keyManager,
    IOptionsMonitor<KyrolusDataProtectionKeyCleanupOptions> options,
    ILogger<KyrolusDataProtectionKeyCleanupService> logger)
    : BackgroundService
{
    private readonly IKeyManager keyManager = keyManager ?? throw new ArgumentNullException(nameof(keyManager));
    private readonly IOptionsMonitor<KyrolusDataProtectionKeyCleanupOptions> options =
        options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<KyrolusDataProtectionKeyCleanupService> logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var current = options.CurrentValue;
            if (!current.Enabled)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
                continue;
            }

            await CleanupOnceAsync(current, stoppingToken).ConfigureAwait(false);

            var delay = current.Interval <= TimeSpan.Zero ? TimeSpan.FromHours(6) : current.Interval;
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }

    private Task CleanupOnceAsync(KyrolusDataProtectionKeyCleanupOptions current, CancellationToken cancellationToken)
    {
        if (keyManager is not IDeletableKeyManager deletable || !deletable.CanDeleteKeys)
        {
            logger.LogDebug("Key cleanup skipped because the current key manager does not support deletion.");
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.UtcNow;
        var deleted = deletable.DeleteKeys(key =>
        {
            if (current.DeleteExpiredKeys && key.ExpirationDate < now - current.ExpiredKeyGracePeriod)
            {
                return true;
            }

            if (current.DeleteRevokedKeys &&
                key.IsRevoked &&
                key.CreationDate < now - current.RevokedKeyGracePeriod)
            {
                return true;
            }

            return false;
        });

        if (deleted)
        {
            logger.LogInformation("Expired or revoked data protection keys were cleaned up.");
        }

        return Task.CompletedTask;
    }
}
