using Azure.Storage.Blobs;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.DataProtection.AzureStorage;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionAzureBlobStorage(
        this KyrolusDataProtectionBuilder builder,
        string connectionString,
        string containerName,
        string blobName = "dataprotection-keys.xml")
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("Container name is required.", nameof(containerName));
        }

        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("Blob name is required.", nameof(blobName));
        }

        var blobClient = new BlobClient(connectionString, containerName, blobName);
        builder.DataProtection.PersistKeysToAzureBlobStorage(blobClient);
        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionAzureBlobStorage(
        this KyrolusDataProtectionBuilder builder,
        BlobContainerClient containerClient,
        string blobName = "dataprotection-keys.xml")
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (containerClient is null) throw new ArgumentNullException(nameof(containerClient));
        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("Blob name is required.", nameof(blobName));
        }

        var blobClient = containerClient.GetBlobClient(blobName);
        builder.DataProtection.PersistKeysToAzureBlobStorage(blobClient);
        return builder;
    }
}
