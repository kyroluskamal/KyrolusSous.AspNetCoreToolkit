namespace KyrolusSous.Storage.Abstractions;

/// <summary>
/// Scoped container abstraction allowing operations directly within a designated container/bucket.
/// </summary>
public interface IKyrolusBlobContainer
{
    string ContainerName { get; }

    Task<KyrolusBlobProperties> UploadAsync(string blobName, Stream contentStream, KyrolusBlobDescriptor? descriptor = null, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken = default);
    Task<KyrolusBlobProperties?> GetPropertiesAsync(string blobName, CancellationToken cancellationToken = default);
    Task<string> GetPresignedUrlAsync(string blobName, TimeSpan expiry, bool isWrite = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusBlobProperties>> ListBlobsAsync(string? prefix = null, CancellationToken cancellationToken = default);
    Task<KyrolusBlobProperties> CopyAsync(string sourceBlobName, string destinationBlobName, CancellationToken cancellationToken = default);
    Task<KyrolusBlobProperties> MoveAsync(string sourceBlobName, string destinationBlobName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options and metadata provided when uploading a blob.
/// </summary>
public sealed record KyrolusBlobDescriptor
{
    public string? ContentType { get; init; }
    public IDictionary<string, string>? Metadata { get; init; }
    public bool Compress { get; init; }
    public bool Encrypt { get; init; }
}

/// <summary>
/// Properties and metadata of a stored blob.
/// </summary>
public sealed record KyrolusBlobProperties
{
    public required string ContainerName { get; init; }
    public required string BlobName { get; init; }
    public long ContentLength { get; init; }
    public string? ContentType { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents an active multipart upload session.
/// </summary>
public sealed record KyrolusMultipartUploadSession
{
    public required string UploadId { get; init; }
    public required string ContainerName { get; init; }
    public required string BlobName { get; init; }
    public DateTimeOffset InitiatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a single part/chunk in a multipart upload.
/// </summary>
public sealed record KyrolusMultipartPartInfo
{
    public required int PartNumber { get; init; }
    public required string ETag { get; init; }
    public long Size { get; init; }
}

/// <summary>
/// Provider supporting multipart and chunked uploads for large blobs.
/// </summary>
public interface IKyrolusMultipartStorageProvider
{
    Task<KyrolusMultipartUploadSession> InitiateMultipartUploadAsync(string containerName, string blobName, KyrolusBlobDescriptor? descriptor = null, CancellationToken cancellationToken = default);
    Task<KyrolusMultipartPartInfo> UploadPartAsync(KyrolusMultipartUploadSession session, int partNumber, Stream partStream, CancellationToken cancellationToken = default);
    Task<KyrolusBlobProperties> CompleteMultipartUploadAsync(KyrolusMultipartUploadSession session, IEnumerable<KyrolusMultipartPartInfo> parts, CancellationToken cancellationToken = default);
    Task<bool> AbortMultipartUploadAsync(KyrolusMultipartUploadSession session, CancellationToken cancellationToken = default);
}

