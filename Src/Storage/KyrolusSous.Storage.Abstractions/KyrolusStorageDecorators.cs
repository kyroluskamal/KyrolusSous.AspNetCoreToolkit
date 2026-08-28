using KyrolusSous.Compression;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.Storage.Abstractions;

/// <summary>
/// Transparently compresses uploaded blobs and decompresses downloaded blobs using unified <see cref="IKyrolusCompressor"/>.
/// </summary>
public class KyrolusCompressedStorageDecorator(
    IKyrolusStorageProvider inner,
    IKyrolusCompressor compressor) : IKyrolusStorageProvider
{
    private readonly IKyrolusStorageProvider _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IKyrolusCompressor _compressor = compressor ?? throw new ArgumentNullException(nameof(compressor));

    public async Task<KyrolusBlobProperties> UploadAsync(string containerName, string blobName, Stream contentStream, KyrolusBlobDescriptor? descriptor = null, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await contentStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var rawBytes = memoryStream.ToArray();
        var compressedBytes = _compressor.Compress(rawBytes);

        using var compressedStream = new MemoryStream(compressedBytes);
        var baseDescriptor = descriptor ?? new KyrolusBlobDescriptor();
        var modifiedDescriptor = baseDescriptor with
        {
            Metadata = new Dictionary<string, string>(baseDescriptor.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
            {
                ["kyrolus-compression"] = _compressor.Algorithm.ToString().ToLowerInvariant()
            }
        };

        return await _inner.UploadAsync(containerName, blobName, compressedStream, modifiedDescriptor, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var compressedStream = await _inner.DownloadAsync(containerName, blobName, cancellationToken).ConfigureAwait(false);
        using var memoryStream = new MemoryStream();
        await compressedStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var compressedBytes = memoryStream.ToArray();
        var decompressedBytes = _compressor.Decompress(compressedBytes);
        return new MemoryStream(decompressedBytes);
    }

    public Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        => _inner.DeleteAsync(containerName, blobName, cancellationToken);

    public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        => _inner.ExistsAsync(containerName, blobName, cancellationToken);

    public Task<KyrolusBlobProperties?> GetPropertiesAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        => _inner.GetPropertiesAsync(containerName, blobName, cancellationToken);

    public Task<string> GetPresignedUrlAsync(string containerName, string blobName, TimeSpan expiry, bool isWrite = false, CancellationToken cancellationToken = default)
        => _inner.GetPresignedUrlAsync(containerName, blobName, expiry, isWrite, cancellationToken);

    public Task<IReadOnlyList<KyrolusBlobProperties>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
        => _inner.ListBlobsAsync(containerName, prefix, cancellationToken);

    public Task<KyrolusBlobProperties> CopyBlobAsync(string sourceContainer, string sourceBlob, string destinationContainer, string destinationBlob, CancellationToken cancellationToken = default)
        => _inner.CopyBlobAsync(sourceContainer, sourceBlob, destinationContainer, destinationBlob, cancellationToken);

    public Task<KyrolusBlobProperties> MoveBlobAsync(string sourceContainer, string sourceBlob, string destinationContainer, string destinationBlob, CancellationToken cancellationToken = default)
        => _inner.MoveBlobAsync(sourceContainer, sourceBlob, destinationContainer, destinationBlob, cancellationToken);
}

/// <summary>
/// Transparently encrypts uploaded blobs and decrypts downloaded blobs using ASP.NET Core Data Protection.
/// </summary>
public class KyrolusProtectedStorageDecorator(
    IKyrolusStorageProvider inner,
    IDataProtectionProvider dataProtectionProvider,
    string purpose = "KyrolusSous.Storage.Protection") : IKyrolusStorageProvider
{
    private readonly IKyrolusStorageProvider _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IDataProtector _protector = (dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider))).CreateProtector(purpose);

    public async Task<KyrolusBlobProperties> UploadAsync(string containerName, string blobName, Stream contentStream, KyrolusBlobDescriptor? descriptor = null, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await contentStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var rawBytes = memoryStream.ToArray();
        var encryptedBytes = _protector.Protect(rawBytes);

        using var encryptedStream = new MemoryStream(encryptedBytes);
        var baseDescriptor = descriptor ?? new KyrolusBlobDescriptor();
        var modifiedDescriptor = baseDescriptor with
        {
            Metadata = new Dictionary<string, string>(baseDescriptor.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
            {
                ["kyrolus-encrypted"] = "true"
            }
        };

        return await _inner.UploadAsync(containerName, blobName, encryptedStream, modifiedDescriptor, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var encryptedStream = await _inner.DownloadAsync(containerName, blobName, cancellationToken).ConfigureAwait(false);
        using var memoryStream = new MemoryStream();
        await encryptedStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var encryptedBytes = memoryStream.ToArray();
        var decryptedBytes = _protector.Unprotect(encryptedBytes);
        return new MemoryStream(decryptedBytes);
    }

    public Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        => _inner.DeleteAsync(containerName, blobName, cancellationToken);

    public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        => _inner.ExistsAsync(containerName, blobName, cancellationToken);

    public Task<KyrolusBlobProperties?> GetPropertiesAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        => _inner.GetPropertiesAsync(containerName, blobName, cancellationToken);

    public Task<string> GetPresignedUrlAsync(string containerName, string blobName, TimeSpan expiry, bool isWrite = false, CancellationToken cancellationToken = default)
        => _inner.GetPresignedUrlAsync(containerName, blobName, expiry, isWrite, cancellationToken);

    public Task<IReadOnlyList<KyrolusBlobProperties>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
        => _inner.ListBlobsAsync(containerName, prefix, cancellationToken);

    public Task<KyrolusBlobProperties> CopyBlobAsync(string sourceContainer, string sourceBlob, string destinationContainer, string destinationBlob, CancellationToken cancellationToken = default)
        => _inner.CopyBlobAsync(sourceContainer, sourceBlob, destinationContainer, destinationBlob, cancellationToken);

    public Task<KyrolusBlobProperties> MoveBlobAsync(string sourceContainer, string sourceBlob, string destinationContainer, string destinationBlob, CancellationToken cancellationToken = default)
        => _inner.MoveBlobAsync(sourceContainer, sourceBlob, destinationContainer, destinationBlob, cancellationToken);
}
