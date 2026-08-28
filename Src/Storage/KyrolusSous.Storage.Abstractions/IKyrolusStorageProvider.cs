namespace KyrolusSous.Storage.Abstractions;

/// <summary>
/// Universal contract for enterprise blob and file storage operations across Local FileSystem, S3, and Azure Blob.
/// </summary>
public interface IKyrolusStorageProvider
{
    /// <summary>
    /// Uploads content stream to the specified container and blob name.
    /// </summary>
    Task<KyrolusBlobProperties> UploadAsync(
        string containerName,
        string blobName,
        Stream contentStream,
        KyrolusBlobDescriptor? descriptor = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads blob content stream from the specified container.
    /// </summary>
    Task<Stream> DownloadAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob from the specified container.
    /// </summary>
    Task<bool> DeleteAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a blob exists in the container.
    /// </summary>
    Task<bool> ExistsAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves metadata and properties of a blob.
    /// </summary>
    Task<KyrolusBlobProperties?> GetPropertiesAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a pre-signed URL for direct client upload or download with expiration.
    /// </summary>
    Task<string> GetPresignedUrlAsync(
        string containerName,
        string blobName,
        TimeSpan expiry,
        bool isWrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists blobs in the specified container with optional prefix filter.
    /// </summary>
    Task<IReadOnlyList<KyrolusBlobProperties>> ListBlobsAsync(
        string containerName,
        string? prefix = null,
        CancellationToken cancellationToken = default);
}
