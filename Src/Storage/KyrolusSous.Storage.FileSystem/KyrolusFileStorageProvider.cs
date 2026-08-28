using KyrolusSous.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Storage.FileSystem;

public sealed class KyrolusFileStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "KyrolusStorage");
}

public sealed class KyrolusFileStorageProvider : IKyrolusStorageProvider, IKyrolusMultipartStorageProvider
{
    private readonly string _rootPath;

    public KyrolusFileStorageProvider(IOptions<KyrolusFileStorageOptions> options)
    {
        _rootPath = options?.Value?.RootPath ?? Path.Combine(AppContext.BaseDirectory, "KyrolusStorage");
        Directory.CreateDirectory(_rootPath);
    }

    public KyrolusFileStorageProvider(string rootPath)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath) ? Path.Combine(AppContext.BaseDirectory, "KyrolusStorage") : rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<KyrolusBlobProperties> UploadAsync(
        string containerName,
        string blobName,
        Stream contentStream,
        KyrolusBlobDescriptor? descriptor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentNullException.ThrowIfNull(contentStream);

        var containerDir = Path.Combine(_rootPath, SanitizePath(containerName));
        Directory.CreateDirectory(containerDir);

        var filePath = Path.Combine(containerDir, SanitizePath(blobName));
        var dirName = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dirName))
        {
            Directory.CreateDirectory(dirName);
        }

        var tempPath = filePath + ".tmp." + Guid.NewGuid().ToString("N");
        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, filePath, overwrite: true);

        var fileInfo = new FileInfo(filePath);
        return new KyrolusBlobProperties
        {
            ContainerName = containerName,
            BlobName = blobName,
            ContentLength = fileInfo.Length,
            ContentType = descriptor?.ContentType ?? GetMimeType(blobName),
            LastModified = fileInfo.LastWriteTimeUtc,
            Metadata = descriptor?.Metadata ?? new Dictionary<string, string>()
        };
    }

    public Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Blob '{blobName}' does not exist in container '{containerName}'.", filePath);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<Stream>(stream);
    }

    public async Task<Stream> DownloadRangeAsync(string containerName, string blobName, long offset, long length, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Blob '{blobName}' does not exist in container '{containerName}'.", filePath);
        }

        var memoryStream = new MemoryStream((int)length);
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
        {
            fileStream.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[Math.Min(81920, length)];
            long bytesRemaining = length;

            while (bytesRemaining > 0)
            {
                var bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                var read = await fileStream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;

                await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                bytesRemaining -= read;
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    public Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task<KyrolusBlobProperties?> GetPropertiesAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        if (!File.Exists(filePath))
        {
            return Task.FromResult<KyrolusBlobProperties?>(null);
        }

        var fileInfo = new FileInfo(filePath);
        var props = new KyrolusBlobProperties
        {
            ContainerName = containerName,
            BlobName = blobName,
            ContentLength = fileInfo.Length,
            ContentType = GetMimeType(blobName),
            LastModified = fileInfo.LastWriteTimeUtc
        };

        return Task.FromResult<KyrolusBlobProperties?>(props);
    }

    public Task<string> GetPresignedUrlAsync(string containerName, string blobName, TimeSpan expiry, bool isWrite = false, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        return Task.FromResult(new Uri(filePath).AbsoluteUri);
    }

    public Task<IReadOnlyList<KyrolusBlobProperties>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        var containerDir = Path.Combine(_rootPath, SanitizePath(containerName));
        if (!Directory.Exists(containerDir))
        {
            return Task.FromResult<IReadOnlyList<KyrolusBlobProperties>>([]);
        }

        var files = Directory.GetFiles(containerDir, "*", SearchOption.AllDirectories);
        var result = new List<KyrolusBlobProperties>();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(containerDir, file).Replace('\\', '/');
            if (prefix is not null && !relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileInfo = new FileInfo(file);
            result.Add(new KyrolusBlobProperties
            {
                ContainerName = containerName,
                BlobName = relativePath,
                ContentLength = fileInfo.Length,
                ContentType = GetMimeType(relativePath),
                LastModified = fileInfo.LastWriteTimeUtc
            });
        }

        return Task.FromResult<IReadOnlyList<KyrolusBlobProperties>>(result);
    }

    public async Task<KyrolusBlobProperties> CopyBlobAsync(string sourceContainer, string sourceBlob, string destinationContainer, string destinationBlob, CancellationToken cancellationToken = default)
    {
        var srcPath = GetFilePath(sourceContainer, sourceBlob);
        var destPath = GetFilePath(destinationContainer, destinationBlob);

        if (!File.Exists(srcPath))
        {
            throw new FileNotFoundException($"Source blob '{sourceBlob}' in container '{sourceContainer}' was not found.");
        }

        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        File.Copy(srcPath, destPath, overwrite: true);

        var srcProps = await GetPropertiesAsync(sourceContainer, sourceBlob, cancellationToken).ConfigureAwait(false);
        return new KyrolusBlobProperties
        {
            ContainerName = destinationContainer,
            BlobName = destinationBlob,
            ContentLength = srcProps?.ContentLength ?? 0,
            ContentType = srcProps?.ContentType ?? GetMimeType(destinationBlob),
            LastModified = DateTimeOffset.UtcNow,
            Metadata = srcProps?.Metadata ?? new Dictionary<string, string>()
        };
    }

    public async Task<KyrolusBlobProperties> MoveBlobAsync(string sourceContainer, string sourceBlob, string destinationContainer, string destinationBlob, CancellationToken cancellationToken = default)
    {
        var result = await CopyBlobAsync(sourceContainer, sourceBlob, destinationContainer, destinationBlob, cancellationToken).ConfigureAwait(false);
        await DeleteAsync(sourceContainer, sourceBlob, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public Task<KyrolusMultipartUploadSession> InitiateMultipartUploadAsync(string containerName, string blobName, KyrolusBlobDescriptor? descriptor = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var uploadId = Guid.NewGuid().ToString("N");
        var partsDir = Path.Combine(_rootPath, ".multipart", uploadId);
        Directory.CreateDirectory(partsDir);

        return Task.FromResult(new KyrolusMultipartUploadSession
        {
            UploadId = uploadId,
            ContainerName = containerName,
            BlobName = blobName
        });
    }

    public async Task<KyrolusMultipartPartInfo> UploadPartAsync(KyrolusMultipartUploadSession session, int partNumber, Stream partStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(partStream);

        var partsDir = Path.Combine(_rootPath, ".multipart", session.UploadId);
        Directory.CreateDirectory(partsDir);

        var partPath = Path.Combine(partsDir, $"part_{partNumber:D6}.dat");
        using (var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await partStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        var partInfo = new FileInfo(partPath);
        return new KyrolusMultipartPartInfo
        {
            PartNumber = partNumber,
            ETag = $"\"part-{partNumber}-{partInfo.Length}\"",
            Size = partInfo.Length
        };
    }

    public async Task<KyrolusBlobProperties> CompleteMultipartUploadAsync(KyrolusMultipartUploadSession session, IEnumerable<KyrolusMultipartPartInfo> parts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var sortedParts = parts.OrderBy(p => p.PartNumber).ToList();

        var containerDir = Path.Combine(_rootPath, SanitizePath(session.ContainerName));
        Directory.CreateDirectory(containerDir);
        var finalPath = Path.Combine(containerDir, SanitizePath(session.BlobName));

        var dirName = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(dirName))
        {
            Directory.CreateDirectory(dirName);
        }

        var tempPath = finalPath + ".tmp." + Guid.NewGuid().ToString("N");
        var partsDir = Path.Combine(_rootPath, ".multipart", session.UploadId);

        using (var finalFs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            foreach (var part in sortedParts)
            {
                var partPath = Path.Combine(partsDir, $"part_{part.PartNumber:D6}.dat");
                if (File.Exists(partPath))
                {
                    using var partFs = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                    await partFs.CopyToAsync(finalFs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        File.Move(tempPath, finalPath, overwrite: true);

        if (Directory.Exists(partsDir))
        {
            Directory.Delete(partsDir, recursive: true);
        }

        var fileInfo = new FileInfo(finalPath);
        return new KyrolusBlobProperties
        {
            ContainerName = session.ContainerName,
            BlobName = session.BlobName,
            ContentLength = fileInfo.Length,
            ContentType = GetMimeType(session.BlobName),
            LastModified = fileInfo.LastWriteTimeUtc
        };
    }

    public Task<bool> AbortMultipartUploadAsync(KyrolusMultipartUploadSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var partsDir = Path.Combine(_rootPath, ".multipart", session.UploadId);
        if (Directory.Exists(partsDir))
        {
            Directory.Delete(partsDir, recursive: true);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private string GetFilePath(string containerName, string blobName)
    {
        return Path.Combine(_rootPath, SanitizePath(containerName), SanitizePath(blobName));
    }

    private static string SanitizePath(string name)
    {
        return name.TrimStart('/', '\\').Replace("..", "");
    }

    private static string GetMimeType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFileStorage(this IServiceCollection services, Action<KyrolusFileStorageOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IKyrolusStorageProvider, KyrolusFileStorageProvider>();
        services.AddSingleton<IKyrolusMultipartStorageProvider>(sp => (KyrolusFileStorageProvider)sp.GetRequiredService<IKyrolusStorageProvider>());
        return services;
    }
}
