namespace KyrolusSous.DataProtection.Abstractions;

public sealed class KyrolusDataProtectionTenantOptions
{
    public string PurposePrefix { get; set; } = "tenant";
    public bool UseTenantPrefix { get; set; } = true;
}
