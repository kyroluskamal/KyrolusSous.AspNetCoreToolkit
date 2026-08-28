namespace KyrolusSous.DataProtection.Abstractions;

/// <summary>
/// Options for automated background key rotation.
/// </summary>
public sealed class KyrolusDataProtectionKeyRotationOptions
{
    /// <summary>
    /// Gets or sets whether automated key rotation is enabled. Default is false.
    /// </summary>
    public bool EnableAutoRotation { get; set; } = false;

    /// <summary>
    /// Gets or sets how frequently the background worker checks key expiration. Default is 6 hours.
    /// </summary>
    public TimeSpan RotationCheckInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Gets or sets the threshold before expiration at which a new key should be generated. Default is 2 days.
    /// </summary>
    public TimeSpan RotateBeforeExpiryThreshold { get; set; } = TimeSpan.FromDays(2);
}
