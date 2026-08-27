using System.Security.Cryptography.X509Certificates;
using KyrolusSous.Auth.OpenIddict.Options;

namespace KyrolusSous.Auth.OpenIddict.Config;

/// <summary>
/// Loads an X.509 certificate from whichever source the options describe.
/// </summary>
internal static class KyrolusCertificateResolver
{
    /// <summary>
    /// Resolves the certificate, or returns <c>null</c> when no source is configured.
    /// </summary>
    /// <param name="options">The certificate source.</param>
    /// <param name="purpose">What the certificate is for, used in error messages ("signing"/"encryption").</param>
    public static X509Certificate2? Resolve(KyrolusOpenIddictCertificateOptions options, string purpose)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Certificate is not null)
        {
            return options.Certificate;
        }

        if (!string.IsNullOrWhiteSpace(options.Thumbprint))
        {
            return LoadFromStore(options, purpose);
        }

        if (!string.IsNullOrWhiteSpace(options.Base64))
        {
            byte[] raw;
            try
            {
                raw = Convert.FromBase64String(options.Base64);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    $"The {purpose} certificate Base64 value is not valid base64.", exception);
            }

            return Load(raw, options, purpose);
        }

        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            if (!File.Exists(options.FilePath))
            {
                throw new FileNotFoundException(
                    $"The {purpose} certificate file was not found at '{options.FilePath}'.", options.FilePath);
            }

            return Load(File.ReadAllBytes(options.FilePath), options, purpose);
        }

        return null;
    }

    private static X509Certificate2 Load(byte[] raw, KyrolusOpenIddictCertificateOptions options, string purpose)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(raw, options.Password, ResolveFlags(options));
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"The {purpose} certificate could not be loaded. Check the password and that the blob is a " +
                "PKCS#12 (.pfx) archive containing a private key.", exception);
        }
    }

    private static X509Certificate2 LoadFromStore(KyrolusOpenIddictCertificateOptions options, string purpose)
    {
        using var store = new X509Store(options.StoreName, options.StoreLocation);
        store.Open(OpenFlags.ReadOnly);

        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, options.Thumbprint!, validOnly: false);
        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"No {purpose} certificate with thumbprint '{options.Thumbprint}' was found in " +
                $"{options.StoreLocation}/{options.StoreName}.");
        }

        var certificate = matches[0];
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"The {purpose} certificate with thumbprint '{options.Thumbprint}' has no private key, " +
                "so it cannot be used to issue tokens.");
        }

        return certificate;
    }

    private static X509KeyStorageFlags ResolveFlags(KyrolusOpenIddictCertificateOptions options)
    {
        if (options.KeyStorageFlags is { } explicitFlags)
        {
            return explicitFlags;
        }

        // MachineKeySet needs a machine key store to write into. A Linux container has none, so
        // the load fails there with a bare "The system cannot find the file specified" that says
        // nothing about the real cause. EphemeralKeySet keeps the key in memory instead, which is
        // exactly right for a certificate mounted read-only into a container.
        return OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable
            : X509KeyStorageFlags.EphemeralKeySet;
    }
}
