using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.DataProtection.Vault;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Encrypts DataProtection keys using HashiCorp Vault's Transit secrets engine.
    /// </summary>
    public static KyrolusDataProtectionBuilder ProtectKeysWithVault(
        this KyrolusDataProtectionBuilder builder,
        Action<KyrolusVaultOptions> configure,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusVaultOptions();
        configure(options);

        var client = httpClient ?? new HttpClient { Timeout = options.Timeout };
        var encryptor = new KyrolusVaultXmlEncryptor(client, options);

        builder.Services.AddSingleton<IXmlEncryptor>(encryptor);
        builder.Services.Configure<KeyManagementOptions>(opt => opt.XmlEncryptor = encryptor);

        return builder;
    }
}
