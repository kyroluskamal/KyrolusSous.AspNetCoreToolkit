using KyrolusSous.RabbitMQ.Abstractions.Security;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.RabbitMQ.Runtime.Security;

/// <summary>
/// Message encryptor integrated directly with the <see cref="IDataProtectionProvider"/> and KyrolusSous.DataProtection key ring.
/// </summary>
public class KyrolusDataProtectionMessageEncryptor : IKyrolusMessageEncryptor
{
    private readonly IDataProtector _protector;

    public KyrolusDataProtectionMessageEncryptor(IDataProtectionProvider dataProtectionProvider, string purpose = "KyrolusSous.RabbitMQ.Payloads")
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        _protector = dataProtectionProvider.CreateProtector(purpose);
    }

    public KyrolusDataProtectionMessageEncryptor(IDataProtector protector)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public byte[] Encrypt(byte[] plainBytes)
    {
        ArgumentNullException.ThrowIfNull(plainBytes);
        return _protector.Protect(plainBytes);
    }

    public byte[] Decrypt(byte[] cipherBytes)
    {
        ArgumentNullException.ThrowIfNull(cipherBytes);
        return _protector.Unprotect(cipherBytes);
    }

    public ValueTask<byte[]> EncryptAsync(byte[] plainBytes, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Encrypt(plainBytes));
    }

    public ValueTask<byte[]> DecryptAsync(byte[] cipherBytes, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Decrypt(cipherBytes));
    }
}
