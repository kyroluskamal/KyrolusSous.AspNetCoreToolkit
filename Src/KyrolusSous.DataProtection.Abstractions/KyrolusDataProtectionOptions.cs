namespace KyrolusSous.DataProtection.Abstractions;

public sealed class KyrolusDataProtectionOptions
{
    public string ApplicationName { get; set; } = "default";
    public TimeSpan? DefaultKeyLifetime { get; set; }
    public bool? AutoGenerateKeys { get; set; }
    public KyrolusKeyProtectionOptions? KeyProtection { get; set; }
}
