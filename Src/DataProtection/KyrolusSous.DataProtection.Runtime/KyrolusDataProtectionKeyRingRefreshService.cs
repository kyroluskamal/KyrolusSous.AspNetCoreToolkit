using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionKeyRingRefreshService(
    IKeyManager keyManager,
    IEnumerable<IKyrolusKeyRingRefreshHook> hooks,
    IOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions> options,
    ILogger<KyrolusDataProtectionKeyRingRefreshService> logger)
    : BackgroundService
{
    private readonly IKeyManager keyManager = keyManager ?? throw new ArgumentNullException(nameof(keyManager));
    private readonly IEnumerable<IKyrolusKeyRingRefreshHook> hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
    private readonly IOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions> options =
        options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<KyrolusDataProtectionKeyRingRefreshService> logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastRefresh = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            var current = options.CurrentValue;
            if (!current.Enabled)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var token = keyManager.GetCacheExpirationToken();
            await WaitForTokenAsync(token, stoppingToken).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var now = DateTimeOffset.UtcNow;
            if (now - lastRefresh < current.MinimumInterval)
            {
                continue;
            }

            lastRefresh = now;
            await NotifyHooksAsync(current, now, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task NotifyHooksAsync(
        KyrolusDataProtectionKeyRingRefreshOptions current,
        DateTimeOffset refreshedAt,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<KyrolusDataProtectionKeyInfo>? keys = null;
        if (current.IncludeKeyDetails)
        {
            keys = keyManager.GetAllKeys()
                .Select(MapKey)
                .ToArray();
        }

        var context = new KyrolusKeyRingRefreshContext(refreshedAt, keys);

        foreach (var hook in hooks)
        {
            try
            {
                await hook.OnKeyRingRefreshedAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Key ring refresh hook failed.");
            }
        }
    }

    private static KyrolusDataProtectionKeyInfo MapKey(IKey key)
    {
        return new KyrolusDataProtectionKeyInfo(
            key.KeyId,
            key.ActivationDate,
            key.ExpirationDate,
            key.CreationDate,
            RevokedAt: null,
            key.IsRevoked);
    }

    private static Task WaitForTokenAsync(CancellationToken token, CancellationToken stoppingToken)
    {
        if (token.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg1 = token.Register(() => tcs.TrySetResult());
        using var reg2 = stoppingToken.Register(() => tcs.TrySetCanceled(stoppingToken));
        return tcs.Task;
    }
}
