using System.Security.Cryptography.X509Certificates;

namespace KyrolusSous.DataProtection.Abstractions;

public enum KyrolusKeyProtectionKind
{
    None,
    Dpapi,
    Certificate
}

public sealed class KyrolusKeyProtectionOptions
{
    public KyrolusKeyProtectionKind Kind { get; set; } = KyrolusKeyProtectionKind.None;
    public bool UseMachineStore { get; set; } = true;
    public string? CertificateThumbprint { get; set; }
    public StoreName StoreName { get; set; } = StoreName.My;
    public StoreLocation StoreLocation { get; set; } = StoreLocation.CurrentUser;
}
