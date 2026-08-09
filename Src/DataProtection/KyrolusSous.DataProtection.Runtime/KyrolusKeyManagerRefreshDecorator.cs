using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusKeyManagerRefreshDecorator(
    IKeyManager inner,
    KyrolusKeyRingRefreshTokenSource tokenSource,
    IKyrolusKeyRingRefreshNotifier notifier,
    IOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions> refreshOptions,
    IOptions<KyrolusDataProtectionOptions> dataProtectionOptions,
    KyrolusDataProtectionInstanceId instanceId,
    ILogger<KyrolusKeyManagerRefreshDecorator> logger)
    : IKeyManager
{
    private readonly IKeyManager inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly KyrolusKeyRingRefreshTokenSource tokenSource =
        tokenSource ?? throw new ArgumentNullException(nameof(tokenSource));
    private readonly IKyrolusKeyRingRefreshNotifier notifier =
        notifier ?? throw new ArgumentNullException(nameof(notifier));
    private readonly IOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions> refreshOptions =
        refreshOptions ?? throw new ArgumentNullException(nameof(refreshOptions));
    private readonly KyrolusDataProtectionOptions dataProtectionOptions =
        dataProtectionOptions?.Value ?? throw new ArgumentNullException(nameof(dataProtectionOptions));
    private readonly KyrolusDataProtectionInstanceId instanceId =
        instanceId ?? throw new ArgumentNullException(nameof(instanceId));
    private readonly ILogger<KyrolusKeyManagerRefreshDecorator> logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public IReadOnlyCollection<IKey> GetAllKeys()
        => inner.GetAllKeys();

    public IKey CreateNewKey(DateTimeOffset activationDate, DateTimeOffset expirationDate)
    {
        var key = inner.CreateNewKey(activationDate, expirationDate);
        PublishRefresh(KyrolusKeyRingRefreshReason.KeyCreated);
        return key;
    }

    public void RevokeKey(Guid keyId, string? reason = null)
    {
        inner.RevokeKey(keyId, reason);
        PublishRefresh(KyrolusKeyRingRefreshReason.KeyRevoked);
    }

    public void RevokeAllKeys(DateTimeOffset revocationDate, string? reason = null)
    {
        inner.RevokeAllKeys(revocationDate, reason);
        PublishRefresh(KyrolusKeyRingRefreshReason.KeyRevoked);
    }

    public CancellationToken GetCacheExpirationToken()
    {
        var innerToken = inner.GetCacheExpirationToken();
        return tokenSource.GetToken(innerToken);
    }

    private void PublishRefresh(KyrolusKeyRingRefreshReason reason)
    {
        var current = refreshOptions.CurrentValue;
        if (!current.EnableCrossInstanceNotifications || !current.PublishLocalChanges)
        {
            return;
        }

        var signal = new KyrolusKeyRingRefreshSignal(
            dataProtectionOptions.ApplicationName,
            instanceId.Value,
            DateTimeOffset.UtcNow,
            reason);

        _ = PublishAsync(signal);
    }

    private async Task PublishAsync(KyrolusKeyRingRefreshSignal signal)
    {
        try
        {
            await notifier.PublishAsync(signal).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish key ring refresh signal.");
        }
    }
}
