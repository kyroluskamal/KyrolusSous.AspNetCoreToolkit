namespace KyrolusSous.DataProtection.Abstractions;

public sealed class KyrolusDataProtectionKeyCleanupOptions
{
    public bool Enabled { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
    public bool DeleteExpiredKeys { get; set; } = true;
    public bool DeleteRevokedKeys { get; set; } = true;
    public TimeSpan ExpiredKeyGracePeriod { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan RevokedKeyGracePeriod { get; set; } = TimeSpan.Zero;
}
