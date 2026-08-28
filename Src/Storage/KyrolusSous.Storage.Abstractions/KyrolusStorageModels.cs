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
