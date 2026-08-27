using System.Security.Cryptography.X509Certificates;

namespace KyrolusSous.Auth.OpenIddict.Options;

/// <summary>
/// Where one of the server certificates comes from. Exactly one source may be set.
/// </summary>
/// <remarks>
/// A signing certificate proves tokens came from this server; an encryption certificate keeps
/// their contents opaque to everyone but this server. They are separate keys with separate
/// rotation schedules, which is why each gets its own instance of this type.
/// </remarks>
public sealed class KyrolusOpenIddictCertificateOptions
{
    /// <summary>
    /// Gets or sets the path to a PKCS#12 (<c>.pfx</c>) file.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets a base64-encoded PKCS#12 blob, for deployments that inject certificates as
    /// environment variables or secrets rather than files.
    /// </summary>
    public string? Base64 { get; set; }

    /// <summary>
    /// Gets or sets the password protecting <see cref="FilePath"/> or <see cref="Base64"/>.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the thumbprint of a certificate to load from the operating system store.
    /// </summary>
    public string? Thumbprint { get; set; }

    /// <summary>Gets or sets the store to search when <see cref="Thumbprint"/> is set. Defaults to <c>My</c>.</summary>
    public StoreName StoreName { get; set; } = StoreName.My;

    /// <summary>
    /// Gets or sets the store location to search when <see cref="Thumbprint"/> is set.
    /// Defaults to <c>CurrentUser</c>.
    /// </summary>
    public StoreLocation StoreLocation { get; set; } = StoreLocation.CurrentUser;

    /// <summary>
    /// Gets or sets an already-loaded certificate. Takes precedence over every other source,
    /// and is the right hook for a key vault client that hands back an
    /// <see cref="X509Certificate2"/> directly.
    /// </summary>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// Gets or sets the key storage flags used when loading from <see cref="FilePath"/> or
    /// <see cref="Base64"/>. Leave <c>null</c> for an OS-appropriate default:
    /// <c>MachineKeySet | PersistKeySet | Exportable</c> on Windows, <c>EphemeralKeySet</c>
    /// elsewhere, because a Linux container has no machine key store to persist into.
    /// </summary>
    public X509KeyStorageFlags? KeyStorageFlags { get; set; }

    /// <summary>Gets whether any source has been configured.</summary>
    public bool IsConfigured =>
        Certificate is not null ||
        !string.IsNullOrWhiteSpace(FilePath) ||
        !string.IsNullOrWhiteSpace(Base64) ||
        !string.IsNullOrWhiteSpace(Thumbprint);
}
