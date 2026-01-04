namespace KyrolusSous.DataProtection.Abstractions;

public sealed class KyrolusDataProtectionInstrumentationOptions
{
    public string ActivitySourceName { get; set; } = "KyrolusSous.DataProtection";
    public string MeterName { get; set; } = "KyrolusSous.DataProtection";
    public bool EnableActivities { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
}
