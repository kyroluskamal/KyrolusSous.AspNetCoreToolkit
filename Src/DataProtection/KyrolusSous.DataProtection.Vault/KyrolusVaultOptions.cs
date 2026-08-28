namespace KyrolusSous.DataProtection.Vault;

/// <summary>
/// Options for configuring HashiCorp Vault Transit key protection.
/// </summary>
public sealed class KyrolusVaultOptions
{
    /// <summary>
    /// Gets or sets the base address of the HashiCorp Vault server (e.g. "https://vault.example.com:8200").
    /// </summary>
    public string VaultAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Vault authentication token.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transit key name in Vault. Default is "dataprotection".
    /// </summary>
    public string KeyName { get; set; } = "dataprotection";

    /// <summary>
    /// Gets or sets the mount path of the Transit secrets engine in Vault. Default is "transit".
    /// </summary>
    public string MountPath { get; set; } = "transit";

    /// <summary>
    /// Gets or sets the request timeout for Vault API calls. Default is 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
