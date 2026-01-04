namespace KyrolusSous.DataProtection.Abstractions;

public sealed class KyrolusDataProtectionKeyEscrowEncryptionOptions
{
    public bool Enabled { get; set; } = true;
    public byte[]? EncryptionKey { get; set; }
    public string? EncryptionKeyBase64 { get; set; }
    public string PayloadPrefix { get; set; } = "kyrolus-escrow:v1:";
}
