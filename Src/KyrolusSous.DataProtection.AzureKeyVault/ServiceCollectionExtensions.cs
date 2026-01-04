using System;
using Azure.Core;
using Azure.Identity;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.AzureKeyVault;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionAzureKeyVault(
        this KyrolusDataProtectionBuilder builder,
        Uri keyIdentifier,
        TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(keyIdentifier);
        ArgumentNullException.ThrowIfNull(credential);

        builder.DataProtection.ProtectKeysWithAzureKeyVault(keyIdentifier, credential);
        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionAzureKeyVault(
        this KyrolusDataProtectionBuilder builder,
        string keyIdentifier,
        TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(keyIdentifier))
        {
            throw new ArgumentException("Key identifier is required.", nameof(keyIdentifier));
        }

        return builder.AddKyrolusDataProtectionAzureKeyVault(new Uri(keyIdentifier), credential);
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionAzureKeyVault(
        this KyrolusDataProtectionBuilder builder,
        string keyIdentifier,
        Action<DefaultAzureCredentialOptions>? configureCredential)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(keyIdentifier))
        {
            throw new ArgumentException("Key identifier is required.", nameof(keyIdentifier));
        }

        var options = new DefaultAzureCredentialOptions();
        configureCredential?.Invoke(options);
        var credential = new DefaultAzureCredential(options);

        return builder.AddKyrolusDataProtectionAzureKeyVault(new Uri(keyIdentifier), credential);
    }
}
