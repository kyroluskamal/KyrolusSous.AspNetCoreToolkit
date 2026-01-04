using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionKeyManager(
    IKeyManager keyManager,
    IOptions<KyrolusDataProtectionOptions> options)
    : IKyrolusDataProtectionKeyManager
{
    private readonly IKeyManager keyManager = keyManager ?? throw new ArgumentNullException(nameof(keyManager));
    private readonly KyrolusDataProtectionOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public Task<IReadOnlyList<KyrolusDataProtectionKeyInfo>> GetAllKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = keyManager.GetAllKeys()
            .Select(MapKey)
            .ToArray();
        return Task.FromResult<IReadOnlyList<KyrolusDataProtectionKeyInfo>>(keys);
    }

    public Task<KyrolusDataProtectionKeyInfo?> GetKeyAsync(Guid keyId, CancellationToken cancellationToken = default)
    {
        var key = keyManager.GetAllKeys().FirstOrDefault(k => k.KeyId == keyId);
        return Task.FromResult(key is null ? null : MapKey(key));
    }

    public Task<KyrolusDataProtectionKeyInfo> CreateKeyAsync(
        DateTimeOffset? activationDate = null,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default)
    {
        var activation = activationDate ?? DateTimeOffset.UtcNow;
        var effectiveLifetime = lifetime ?? options.DefaultKeyLifetime ?? TimeSpan.FromDays(90);
        var expiration = activation + effectiveLifetime;

        var key = CreateNewKey(activation, expiration);
        return Task.FromResult(MapKey(key));
    }

    public Task<KyrolusDataProtectionKeyInfo> RotateKeyAsync(TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
        => CreateKeyAsync(DateTimeOffset.UtcNow, lifetime, cancellationToken);

    public Task RevokeKeyAsync(Guid keyId, string? reason = null, CancellationToken cancellationToken = default)
    {
        keyManager.RevokeKey(keyId, reason);
        return Task.CompletedTask;
    }

    public Task RevokeAllKeysAsync(
        DateTimeOffset? revocationDate = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        keyManager.RevokeAllKeys(revocationDate ?? DateTimeOffset.UtcNow, reason);
        return Task.CompletedTask;
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

    private IKey CreateNewKey(DateTimeOffset activation, DateTimeOffset expiration)
    {
        if (keyManager is IInternalXmlKeyManager internalManager)
        {
            return internalManager.CreateNewKey(Guid.NewGuid(), activation, expiration, DateTimeOffset.UtcNow);
        }

        var direct = keyManager.GetType().GetMethod(
            "CreateNewKey",
            new[] { typeof(DateTimeOffset), typeof(DateTimeOffset) });

        if (direct is not null)
        {
            return (IKey)direct.Invoke(keyManager, new object[] { activation, expiration })!;
        }

        var alt = keyManager.GetType().GetMethod(
            "CreateNewKey",
            new[] { typeof(DateTimeOffset), typeof(string) });

        if (alt is not null)
        {
            return (IKey)alt.Invoke(keyManager, new object?[] { activation, null })!;
        }

        throw new NotSupportedException("IKeyManager.CreateNewKey overload not found.");
    }
}
