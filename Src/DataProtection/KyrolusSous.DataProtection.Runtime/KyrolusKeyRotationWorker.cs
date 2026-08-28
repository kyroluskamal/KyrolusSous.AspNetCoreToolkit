using KyrolusSous.DataProtection.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

/// <summary>
/// Background worker that monitors DataProtection key lifetimes and automatically rotates keys before expiration.
/// </summary>
public sealed class KyrolusKeyRotationWorker(
    IServiceProvider serviceProvider,
    IOptions<KyrolusDataProtectionKeyRotationOptions> options,
    ILogger<KyrolusKeyRotationWorker> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly KyrolusDataProtectionKeyRotationOptions _options = options.Value;
    private readonly ILogger<KyrolusKeyRotationWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableAutoRotation)
        {
            _logger.LogDebug("Kyrolus DataProtection automated key rotation is disabled.");
            return;
        }

        _logger.LogInformation(
            "Kyrolus DataProtection automated key rotation started. Interval: {Interval}, Threshold: {Threshold}",
            _options.RotationCheckInterval,
            _options.RotateBeforeExpiryThreshold);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRotateKeysAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during DataProtection key rotation check.");
            }

            try
            {
                await Task.Delay(_options.RotationCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Checks key expirations and provisions a new key if the active key is approaching expiration.
    /// </summary>
    public async Task<bool> CheckAndRotateKeysAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var keyManager = scope.ServiceProvider.GetService<IKyrolusDataProtectionKeyManager>();

        if (keyManager is null)
        {
            _logger.LogWarning("IKyrolusDataProtectionKeyManager is not registered in service provider.");
            return false;
        }

        var keys = (await keyManager.GetAllKeysAsync(cancellationToken)).ToList();
        if (keys.Count == 0)
        {
            _logger.LogInformation("No existing DataProtection keys found. Provisioning initial key...");
            await keyManager.CreateKeyAsync(null, null, cancellationToken);
            return true;
        }

        var unrevokedKeys = keys.Where(k => !k.IsRevoked).ToList();
        if (unrevokedKeys.Count == 0)
        {
            _logger.LogWarning("All DataProtection keys are revoked. Provisioning new active key...");
            await keyManager.CreateKeyAsync(null, null, cancellationToken);
            return true;
        }

        var latestExpiringKey = unrevokedKeys.MaxBy(k => k.ExpirationDate);
        if (latestExpiringKey is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var remainingTime = latestExpiringKey.ExpirationDate - now;

        // If there is already a future scheduled key that has not expired, skip redundant generation
        if (unrevokedKeys.Any(k => k.ActivationDate > now && k.ExpirationDate > now))
        {
            _logger.LogDebug("A future active DataProtection key is already scheduled. Skipping redundant rotation.");
            return false;
        }

        if (remainingTime <= _options.RotateBeforeExpiryThreshold)
        {
            _logger.LogInformation(
                "Active DataProtection key {KeyId} is expiring in {RemainingTime} (threshold {Threshold}). Generating new key...",
                latestExpiringKey.KeyId,
                remainingTime,
                _options.RotateBeforeExpiryThreshold);

            var newKey = await keyManager.RotateKeyAsync(null, cancellationToken);
            _logger.LogInformation("Successfully rotated DataProtection key. New KeyId: {KeyId}", newKey.KeyId);
            return true;
        }

        _logger.LogDebug(
            "Active DataProtection key {KeyId} is healthy (expires in {RemainingTime}).",
            latestExpiringKey.KeyId,
            remainingTime);

        return false;
    }
}
