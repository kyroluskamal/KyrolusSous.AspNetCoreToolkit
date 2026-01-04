using System;
using Google.Cloud.Kms.V1;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.DataProtection.GoogleKms;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionGoogleKms(
        this KyrolusDataProtectionBuilder builder,
        KeyManagementServiceClient kmsClient,
        string cryptoKeyName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (kmsClient is null) throw new ArgumentNullException(nameof(kmsClient));
        if (string.IsNullOrWhiteSpace(cryptoKeyName))
        {
            throw new ArgumentException("CryptoKey name is required.", nameof(cryptoKeyName));
        }

        builder.Services.TryAddSingleton(kmsClient);
        var options = new KyrolusGcpKmsOptions { CryptoKeyName = cryptoKeyName };
        builder.Services.TryAddSingleton(options);

        var encryptor = new KyrolusGcpKmsXmlEncryptor(kmsClient, options);
        builder.Services.AddSingleton<IXmlEncryptor>(encryptor);
        builder.Services.Configure<KeyManagementOptions>(opt => opt.XmlEncryptor = encryptor);

        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionGoogleKms(
        this KyrolusDataProtectionBuilder builder,
        string cryptoKeyName)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(cryptoKeyName))
        {
            throw new ArgumentException("CryptoKey name is required.", nameof(cryptoKeyName));
        }

        var kmsClient = KeyManagementServiceClient.Create();
        return builder.AddKyrolusDataProtectionGoogleKms(kmsClient, cryptoKeyName);
    }
}
