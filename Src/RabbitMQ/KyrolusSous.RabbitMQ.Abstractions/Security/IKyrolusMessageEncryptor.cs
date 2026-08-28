namespace KyrolusSous.RabbitMQ.Abstractions.Security;

/// <summary>
/// Abstraction for encrypting and decrypting message payloads before transmission and after consumption.
/// </summary>
public interface IKyrolusMessageEncryptor
{
    byte[] Encrypt(byte[] plainBytes);
    byte[] Decrypt(byte[] cipherBytes);
    ValueTask<byte[]> EncryptAsync(byte[] plainBytes, CancellationToken cancellationToken = default);
    ValueTask<byte[]> DecryptAsync(byte[] cipherBytes, CancellationToken cancellationToken = default);
}
