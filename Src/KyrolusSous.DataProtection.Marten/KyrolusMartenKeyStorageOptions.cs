namespace KyrolusSous.DataProtection.Marten;

public sealed class KyrolusMartenKeyStorageOptions
{
    public string? TenantId { get; set; }
    public bool UseLightweightSession { get; set; } = true;
}
