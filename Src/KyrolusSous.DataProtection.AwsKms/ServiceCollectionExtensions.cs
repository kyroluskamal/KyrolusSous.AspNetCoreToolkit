using System;
using System.Collections.Generic;
using Amazon.KeyManagementService;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.DataProtection.AwsKms;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionAwsKms(
        this KyrolusDataProtectionBuilder builder,
        IAmazonKeyManagementService kmsClient,
        string keyId,
        IReadOnlyDictionary<string, string>? encryptionContext = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (kmsClient is null) throw new ArgumentNullException(nameof(kmsClient));
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException("KeyId is required.", nameof(keyId));
        }

        builder.Services.TryAddSingleton(kmsClient);
        var options = new KyrolusAwsKmsOptions
        {
            KeyId = keyId,
            EncryptionContext = encryptionContext
        };

        builder.Services.TryAddSingleton(options);

        var encryptor = new KyrolusAwsKmsXmlEncryptor(kmsClient, options);
        builder.Services.AddSingleton<IXmlEncryptor>(encryptor);
        builder.Services.Configure<KeyManagementOptions>(opt => opt.XmlEncryptor = encryptor);

        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionAwsKms(
        this KyrolusDataProtectionBuilder builder,
        string keyId,
        IReadOnlyDictionary<string, string>? encryptionContext = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException("KeyId is required.", nameof(keyId));
        }

        var kmsClient = new AmazonKeyManagementServiceClient();
        return builder.AddKyrolusDataProtectionAwsKms(kmsClient, keyId, encryptionContext);
    }
}
