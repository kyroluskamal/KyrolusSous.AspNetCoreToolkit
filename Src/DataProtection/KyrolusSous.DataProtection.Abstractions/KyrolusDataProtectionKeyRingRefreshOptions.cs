namespace KyrolusSous.DataProtection.Abstractions;

public sealed class KyrolusDataProtectionKeyRingRefreshOptions
{
    public bool Enabled { get; set; }
    public bool IncludeKeyDetails { get; set; }
    public TimeSpan MinimumInterval { get; set; } = TimeSpan.FromMinutes(1);
    public bool EnableCrossInstanceNotifications { get; set; }
    public bool PublishLocalChanges { get; set; } = true;
    public bool RefreshOnExternalSignal { get; set; } = true;
    public string? InstanceId { get; set; }
}
